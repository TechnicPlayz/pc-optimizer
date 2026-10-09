using System.Runtime.InteropServices;
using VeloraPC.Core;

namespace VeloraPC.Services;

public sealed class CleanLocation
{
    public string FullPath = "";
    public long Bytes;
    public long Items;
}

public sealed class CleanCategory
{
    public string Id = "";
    public string Name = "";
    public string Description = "";
    public long Bytes;
    public long Items;
    public bool Selected = true;
    public List<CleanLocation> Locations = new();
}

public sealed class CleanResult
{
    public long Bytes;
    public int Files;
    public int Skipped;
}

/// <summary>
/// Scans and cleans ONLY a fixed allow-list of system cache locations.
/// Documents, Desktop, Downloads, Pictures and every other personal folder are never touched.
/// Files modified in the last hour are skipped so running installers/apps aren't disturbed.
/// </summary>
public static class Cleanup
{
    static readonly EnumerationOptions Opts = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
    };

    static readonly TimeSpan MinAge = TimeSpan.FromHours(1);

    static IEnumerable<(string Id, string Name, string Desc, string[] Roots)> Definitions()
    {
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var lad = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        yield return ("temp", "Temporary Files",
            "Leftover files that apps and Windows create while running.",
            new[] { Path.GetTempPath(), Path.Combine(win, "Temp") });

        yield return ("bin", "Recycle Bin",
            "Items you have already deleted.",
            Array.Empty<string>());

        yield return ("windows", "Safe Windows Cleanup",
            "Error reports, crash dumps and already-installed update downloads.",
            new[]
            {
                Path.Combine(pd, "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(pd, "Microsoft", "Windows", "WER", "ReportQueue"),
                Path.Combine(lad, "CrashDumps"),
                Path.Combine(win, "Minidump"),
                Path.Combine(win, "SoftwareDistribution", "Download"),
                Path.Combine(win, "ServiceProfiles", "NetworkService", "AppData", "Local",
                             "Microsoft", "Windows", "DeliveryOptimization", "Cache")
            });
    }

    public static (long Bytes, long Items) RecycleBin()
    {
        try
        {
            var info = new Native.SHQUERYRBINFO { cbSize = Marshal.SizeOf<Native.SHQUERYRBINFO>() };
            int hr = Native.SHQueryRecycleBin(null, ref info);
            return hr == 0 ? (info.i64Size, info.i64NumItems) : (0, 0);
        }
        catch { return (0, 0); }
    }

    static (long Bytes, long Items) Measure(string root, CancellationToken ct)
    {
        if (!Directory.Exists(root)) return (0, 0);
        long bytes = 0, items = 0;
        var cutoff = DateTime.UtcNow - MinAge;
        try
        {
            foreach (var f in new DirectoryInfo(root).EnumerateFiles("*", Opts))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (f.LastWriteTimeUtc > cutoff) continue;
                    bytes += f.Length;
                    items++;
                }
                catch { }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }
        return (bytes, items);
    }

    public static Task<List<CleanCategory>> ScanAsync(CancellationToken ct) => Task.Run(() =>
    {
        var result = new List<CleanCategory>();
        foreach (var d in Definitions())
        {
            ct.ThrowIfCancellationRequested();
            var cat = new CleanCategory { Id = d.Id, Name = d.Name, Description = d.Desc };

            if (d.Id == "bin")
            {
                var (b, n) = RecycleBin();
                cat.Bytes = b; cat.Items = n;
                if (n > 0) cat.Locations.Add(new CleanLocation { FullPath = "Recycle Bin (all drives)", Bytes = b, Items = n });
            }
            else
            {
                foreach (var root in d.Roots)
                {
                    var (b, n) = Measure(root, ct);
                    if (b > 0 || n > 0)
                    {
                        cat.Locations.Add(new CleanLocation { FullPath = root, Bytes = b, Items = n });
                        cat.Bytes += b; cat.Items += n;
                    }
                }
            }
            result.Add(cat);
        }
        Logger.Info($"Scan complete: {Fmt.Bytes(result.Sum(c => c.Bytes))} cleanable");
        return result;
    }, ct);

    public static Task<CleanResult> CleanAsync(IEnumerable<CleanCategory> categories,
        IProgress<double>? progress, CancellationToken ct) => Task.Run(() =>
    {
        var res = new CleanResult();
        var selected = categories.Where(c => c.Selected && c.Items > 0).ToList();
        int total = Math.Max(1, selected.Sum(c => c.Id == "bin" ? 1 : c.Locations.Count));
        int done = 0;

        foreach (var cat in selected)
        {
            ct.ThrowIfCancellationRequested();
            if (cat.Id == "bin")
            {
                int hr = Native.SHEmptyRecycleBin(IntPtr.Zero, null, 7);   // no confirm / progress UI / sound
                if (hr >= 0) res.Bytes += cat.Bytes;
                else Logger.Warn($"SHEmptyRecycleBin returned 0x{hr:X}");
                progress?.Report((double)(++done) / total);
                continue;
            }

            foreach (var loc in cat.Locations)
            {
                ct.ThrowIfCancellationRequested();
                DeleteRoot(loc.FullPath, res, ct);
                progress?.Report((double)(++done) / total);
            }
        }
        Logger.Info($"Clean complete: {Fmt.Bytes(res.Bytes)} removed, {res.Skipped} files skipped");
        return res;
    }, ct);

    static void DeleteRoot(string root, CleanResult res, CancellationToken ct)
    {
        if (!Directory.Exists(root)) return;
        var cutoff = DateTime.UtcNow - MinAge;
        var di = new DirectoryInfo(root);
        var files = new List<FileInfo>();

        try { foreach (var f in di.EnumerateFiles("*", Opts)) files.Add(f); }
        catch (Exception ex) { Logger.Warn($"Could not fully enumerate {root}: {ex.Message}"); }

        foreach (var f in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (f.LastWriteTimeUtc > cutoff) continue;
                long len = f.Length;
                if (f.IsReadOnly) f.IsReadOnly = false;
                f.Delete();
                res.Bytes += len;
                res.Files++;
            }
            catch { res.Skipped++; }   // in use or protected: leave it alone
        }

        try
        {
            var dirs = di.EnumerateDirectories("*", Opts).OrderByDescending(d => d.FullName.Length).ToList();
            foreach (var d in dirs)
            {
                try { d.Delete(false); } catch { }   // only succeeds when empty
            }
        }
        catch { }
    }
}
