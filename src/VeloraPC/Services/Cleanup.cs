using System.Diagnostics;
using System.Runtime.InteropServices;
using VeloraPC.Core;

namespace VeloraPC.Services;

public sealed class FileHit
{
    public string Path = "";
    public long Bytes;
}

public sealed class CleanLocation
{
    public string FullPath = "";
    public long Bytes;
    public long Items;
    public List<FileHit> Top = new();      // biggest files here, for review
}

public sealed class CleanCategory
{
    public string Id = "";
    public string Name = "";
    public string Description = "";
    public string Note = "";               // shown when the category has a trade-off
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
    public int Denied;                      // skipped because Windows said "access denied"
}

/// <summary>
/// Storage Cleaner engine. Scans and cleans ONLY a fixed allow-list of cache locations.
/// Personal folders (Documents, Desktop, Downloads, Pictures, ...) can never be cleaned: every folder is
/// checked against a protected list before anything is deleted. Files changed in the last hour are skipped.
/// </summary>
public static class Cleanup
{
    sealed record Def(string Id, string Name, string Desc, string Note, bool DefaultOn, string[] Roots);

    static readonly EnumerationOptions Opts = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
    };

    static readonly TimeSpan MinAge = TimeSpan.FromHours(1);
    static readonly string[] Protected = BuildProtected();

    // ---------------- safety ----------------

    static string Norm(string p) => Path.GetFullPath(p).TrimEnd('\\');

    static bool IsAncestorOrSame(string ancestor, string path) =>
        path.Equals(ancestor, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(ancestor + "\\", StringComparison.OrdinalIgnoreCase);

    static string[] BuildProtected()
    {
        var list = new List<string>();
        void Add(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            try { list.Add(Norm(p)); } catch { }
        }
        var up = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Add(up);
        Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        Add(Path.Combine(up, "Downloads"));
        Add(Path.Combine(up, "OneDrive"));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        return list.ToArray();
    }

    /// <summary>A folder is allowed only if it does not equal or contain any protected personal/system folder.</summary>
    public static bool IsAllowed(string root)
    {
        try
        {
            var r = Norm(root);
            if (r.Length < 10) return false;
            foreach (var p in Protected)
                if (IsAncestorOrSame(r, p)) return false;
            return true;
        }
        catch { return false; }
    }

    // ---------------- definitions ----------------

    static IEnumerable<string> SafeDirs(string path)
    {
        try { return Directory.GetDirectories(path); }
        catch { return Array.Empty<string>(); }
    }

    static IEnumerable<string> BrowserCacheRoots()
    {
        var lad = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var chromium = new[]
        {
            Path.Combine(lad, "Google", "Chrome", "User Data"),
            Path.Combine(lad, "Microsoft", "Edge", "User Data"),
            Path.Combine(lad, "BraveSoftware", "Brave-Browser", "User Data")
        };
        foreach (var data in chromium)
        {
            if (!Directory.Exists(data)) continue;
            foreach (var profile in SafeDirs(data))
            {
                var name = Path.GetFileName(profile);
                if (name != "Default" && !name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var sub in new[] { "Cache", "Code Cache", "GPUCache" })
                {
                    var p = Path.Combine(profile, sub);
                    if (Directory.Exists(p)) yield return p;
                }
            }
        }
        var firefox = Path.Combine(lad, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(firefox))
        {
            foreach (var profile in SafeDirs(firefox))
            {
                var c = Path.Combine(profile, "cache2");
                if (Directory.Exists(c)) yield return c;
            }
        }
    }

    static List<Def> Definitions()
    {
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var lad = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return new List<Def>
        {
            new("temp", "Temporary Files",
                "Leftover files that apps and Windows create while running.", "", true,
                new[] { Path.GetTempPath(), Path.Combine(win, "Temp") }),

            new("browser", "Browser Caches",
                "Cached web pages and images from Chrome, Edge, Brave and Firefox. Passwords, history and logins are not touched.",
                "Websites may load a little slower the first time you revisit them.", true,
                BrowserCacheRoots().ToArray()),

            new("bin", "Recycle Bin", "Items you have already deleted.", "", true, Array.Empty<string>()),

            new("windows", "Safe Windows Cleanup",
                "Error reports, crash dumps and already-installed update downloads.", "", true,
                new[]
                {
                    Path.Combine(pd, "Microsoft", "Windows", "WER", "ReportArchive"),
                    Path.Combine(pd, "Microsoft", "Windows", "WER", "ReportQueue"),
                    Path.Combine(lad, "CrashDumps"),
                    Path.Combine(win, "Minidump"),
                    Path.Combine(win, "SoftwareDistribution", "Download"),
                    Path.Combine(win, "ServiceProfiles", "NetworkService", "AppData", "Local",
                                 "Microsoft", "Windows", "DeliveryOptimization", "Cache")
                }),

            new("shader", "Graphics Shader Caches",
                "Compiled graphics data that games and drivers rebuild automatically.",
                "Off by default: games may stutter briefly the first time you launch them afterwards.", false,
                new[]
                {
                    Path.Combine(lad, "D3DSCache"),
                    Path.Combine(lad, "NVIDIA", "DXCache"),
                    Path.Combine(lad, "NVIDIA", "GLCache"),
                    Path.Combine(lad, "AMD", "DxCache"),
                    Path.Combine(lad, "AMD", "GLCache")
                })
        };
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

    // ---------------- scan ----------------

    static (long Bytes, long Items, List<FileHit> Top) Measure(string root, CancellationToken ct)
    {
        var top = new List<FileHit>();
        if (!IsAllowed(root) || !Directory.Exists(root)) return (0, 0, top);

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
                    long len = f.Length;
                    bytes += len;
                    items++;
                    if (top.Count < 5 || len > top[top.Count - 1].Bytes)
                    {
                        top.Add(new FileHit { Path = f.FullName, Bytes = len });
                        top.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
                        if (top.Count > 5) top.RemoveAt(5);
                    }
                }
                catch { }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }
        return (bytes, items, top);
    }

    public static Task<List<CleanCategory>> ScanAsync(IProgress<TaskProgress>? progress, CancellationToken ct) => Task.Run(() =>
    {
        var defs = Definitions();
        int total = Math.Max(1, defs.Sum(d => d.Id == "bin" ? 1 : d.Roots.Length));
        int done = 0;
        var result = new List<CleanCategory>();

        foreach (var d in defs)
        {
            ct.ThrowIfCancellationRequested();
            var cat = new CleanCategory
            {
                Id = d.Id, Name = d.Name, Description = d.Desc, Note = d.Note, Selected = d.DefaultOn
            };

            progress?.Report(new TaskProgress((double)done / total, $"Scanning {d.Name}…"));
            if (d.Id == "bin")
            {
                var (b, n) = RecycleBin();
                cat.Bytes = b; cat.Items = n;
                if (n > 0) cat.Locations.Add(new CleanLocation { FullPath = "Recycle Bin (all drives)", Bytes = b, Items = n });
                done++;
            }
            else
            {
                foreach (var root in d.Roots)
                {
                    ct.ThrowIfCancellationRequested();
                    var (b, n, top) = Measure(root, ct);
                    if (b > 0 || n > 0)
                    {
                        cat.Locations.Add(new CleanLocation { FullPath = root, Bytes = b, Items = n, Top = top });
                        cat.Bytes += b; cat.Items += n;
                    }
                    done++;
                    progress?.Report(new TaskProgress((double)done / total, $"Scanning {d.Name}…"));
                }
            }
            result.Add(cat);
        }
        progress?.Report(new TaskProgress(1.0, "Scan complete"));
        Logger.Info($"Scan complete: {Fmt.Bytes(result.Sum(c => c.Bytes))} found");
        return result;
    }, ct);

    // ---------------- clean ----------------

    public static Task<CleanResult> CleanAsync(IEnumerable<CleanCategory> categories,
        IProgress<TaskProgress>? progress, CancellationToken ct) => Task.Run(() =>
    {
        var res = new CleanResult();
        var selected = categories.Where(c => c.Selected && c.Items > 0).ToList();
        long totalBytes = Math.Max(1, selected.Sum(c => c.Bytes));
        long processed = 0;
        long lastReport = 0;
        string current = "Preparing…";
        var sw = Stopwatch.StartNew();

        void ReportNow()
        {
            lastReport = sw.ElapsedMilliseconds;
            progress?.Report(new TaskProgress(Math.Clamp((double)processed / totalBytes, 0, 1), current));
        }
        void Advance(long bytes)
        {
            processed += bytes;
            if (sw.ElapsedMilliseconds - lastReport >= 100) ReportNow();
        }

        ReportNow();
        foreach (var cat in selected)
        {
            ct.ThrowIfCancellationRequested();
            current = $"Cleaning {cat.Name}…";
            ReportNow();

            if (cat.Id == "bin")
            {
                int hr = Native.SHEmptyRecycleBin(IntPtr.Zero, null, 7);   // no confirm / progress UI / sound
                if (hr >= 0) res.Bytes += cat.Bytes;
                else Logger.Warn($"SHEmptyRecycleBin returned 0x{hr:X}");
                Advance(cat.Bytes);
                continue;
            }

            foreach (var loc in cat.Locations)
            {
                ct.ThrowIfCancellationRequested();
                if (!IsAllowed(loc.FullPath))
                {
                    Logger.Warn($"Refused to clean protected path: {loc.FullPath}");
                    Advance(loc.Bytes);
                    continue;
                }
                DeleteRoot(loc.FullPath, res, ct, Advance);
            }
        }
        current = "Finishing…";
        processed = totalBytes;
        ReportNow();
        Logger.Info($"Clean complete: {Fmt.Bytes(res.Bytes)} removed, {res.Skipped} skipped ({res.Denied} denied)");
        return res;
    }, ct);

    static void DeleteRoot(string root, CleanResult res, CancellationToken ct, Action<long> advance)
    {
        if (!Directory.Exists(root)) return;
        var cutoff = DateTime.UtcNow - MinAge;
        var di = new DirectoryInfo(root);
        var files = new List<FileInfo>();

        try { foreach (var f in di.EnumerateFiles("*", Opts)) files.Add(f); }
        catch (Exception ex) { Logger.Warn($"Could not fully read {root}: {ex.Message}"); }

        foreach (var f in files)
        {
            ct.ThrowIfCancellationRequested();
            long len = 0;
            try
            {
                if (f.LastWriteTimeUtc > cutoff) continue;      // too new, leave it
                len = f.Length;
                if (f.IsReadOnly) f.IsReadOnly = false;
                f.Delete();
                res.Bytes += len;
                res.Files++;
            }
            catch (UnauthorizedAccessException) { res.Skipped++; res.Denied++; }
            catch { res.Skipped++; }                            // in use: leave it alone
            advance(len);
        }

        try
        {
            var dirs = di.EnumerateDirectories("*", Opts).OrderByDescending(d => d.FullName.Length).ToList();
            foreach (var d in dirs)
            {
                try { d.Delete(false); } catch { }              // only succeeds when empty
            }
        }
        catch { }
    }
}
