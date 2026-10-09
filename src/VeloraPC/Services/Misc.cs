using Microsoft.Win32;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using VeloraPC.Core;

namespace VeloraPC.Services;

public sealed record UpdateInfo(
    bool Checked, bool Available, string? Latest, string? Url, string Message,
    string? AssetUrl = null, string? AssetName = null, long? AssetSize = null,
    string? Sha256 = null, string? Notes = null);

/// <summary>
/// Checks the project's public GitHub releases and installs updates with one click.
/// Safety: HTTPS only, GitHub hosts only, SHA-256 verified when GitHub publishes it, and the user must confirm.
/// </summary>
public static class UpdateService
{
    static readonly HttpClient Http = CreateClient(TimeSpan.FromSeconds(10));
    static readonly HttpClient Downloader = CreateClient(TimeSpan.FromMinutes(10));

    /// <summary>The most recent check that found a newer version (null if up to date / not checked).</summary>
    public static UpdateInfo? Pending { get; private set; }

    static HttpClient CreateClient(TimeSpan timeout)
    {
        var c = new HttpClient { Timeout = timeout };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"VeloraPC/{Branding.VersionText}");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    static bool IsTrusted(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return false;
        var h = u.Host.ToLowerInvariant();
        return h == "github.com" || h == "api.github.com" || h.EndsWith(".githubusercontent.com");
    }

    public static async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await Http.GetAsync($"https://api.github.com/repos/{Branding.Repo}/releases/latest", ct);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return Remember(new UpdateInfo(true, false, null, null, "No releases have been published yet."));
            resp.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var url = root.TryGetProperty("html_url", out var u) ? u.GetString() : Branding.ReleasesUrl;

            string? notes = null;
            if (root.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String)
            {
                notes = (b.GetString() ?? "").Trim();
                if (notes.Length > 700) notes = notes.Substring(0, 700).TrimEnd() + "…";
            }

            string? assetUrl = null, assetName = null, sha = null;
            long? assetSize = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!name.StartsWith("VeloraPC-Setup", StringComparison.OrdinalIgnoreCase) ||
                        !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                    assetName = name;
                    assetUrl = a.TryGetProperty("browser_download_url", out var du) ? du.GetString() : null;
                    assetSize = a.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var sv) ? sv : null;
                    if (a.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String)
                    {
                        var d = dg.GetString() ?? "";
                        if (d.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) sha = d.Substring(7);
                    }
                    break;
                }
            }

            if (System.Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
            {
                var cur = Branding.CurrentVersion;
                var current = new System.Version(cur.Major, cur.Minor, Math.Max(0, cur.Build));
                if (latest > current)
                    return Remember(new UpdateInfo(true, true, latest.ToString(), url, $"Version {latest} is available.",
                        assetUrl, assetName, assetSize, sha, notes));
                return Remember(new UpdateInfo(true, false, latest.ToString(), url, "You're on the latest version."));
            }
            return Remember(new UpdateInfo(true, false, tag, url, "You're up to date."));
        }
        catch (Exception ex)
        {
            Logger.Warn("Update check failed: " + ex.Message);
            return new UpdateInfo(false, false, null, null, "Couldn't check for updates. Check your internet connection.");
        }
    }

    static UpdateInfo Remember(UpdateInfo info)
    {
        Pending = info.Available ? info : null;
        return info;
    }

    /// <summary>Downloads the installer to a temp folder and verifies its checksum when one is published.</summary>
    public static async Task<string> DownloadAsync(UpdateInfo info, IProgress<double>? progress, CancellationToken ct)
    {
        if (info.AssetUrl == null || !IsTrusted(info.AssetUrl))
            throw new InvalidOperationException("No trusted installer is attached to this release.");

        var dir = Path.Combine(Path.GetTempPath(), "VeloraPC-Update");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, Path.GetFileName(info.AssetName ?? "VeloraPC-Setup.exe"));

        using (var resp = await Downloader.GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? info.AssetSize ?? 0;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = File.Create(file);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress?.Report(Math.Min(1.0, (double)done / total));
            }
        }

        if (info.Sha256 != null)
        {
            string hash;
            await using (var fs = File.OpenRead(file))
                hash = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct));
            if (!hash.Equals(info.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(file); } catch { }
                Logger.Error($"Update checksum mismatch (expected {info.Sha256}, got {hash})");
                throw new InvalidDataException("Checksum mismatch");
            }
            Logger.Info("Update checksum verified");
        }
        else Logger.Warn("Release did not publish a checksum; skipped verification");

        return file;
    }

    /// <summary>Starts the installer silently; it closes Velora, installs, and relaunches it.</summary>
    public static bool LaunchInstaller(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path, "/SILENT /CLOSEAPPLICATIONS /NORESTART") { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) { Logger.Error("Could not start the installer", ex); return false; }
    }
}

public static class StartupService
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string TaskName = @"Velora PC\Weekly Maintenance";

    public static bool SetStartWithWindows(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(RunKey);
            if (on) k.SetValue("VeloraPC", $"\"{Environment.ProcessPath}\"");
            else k.DeleteValue("VeloraPC", false);
            return true;
        }
        catch (Exception ex) { Logger.Error("Start-with-Windows change failed", ex); return false; }
    }

    /// <summary>Weekly per-user scheduled task (no admin needed). Runs the app with --auto.</summary>
    public static async Task<bool> SetAutoMaintenanceAsync(bool on)
    {
        var args = on
            ? $"/Create /TN \"{TaskName}\" /TR \"\\\"{Environment.ProcessPath}\\\" --auto\" /SC WEEKLY /D SUN /ST 12:00 /F"
            : $"/Delete /TN \"{TaskName}\" /F";
        var r = await ProcessRunner.RunAsync("schtasks.exe", args);
        return r.ExitCode == 0 || !on;   // deleting a task that doesn't exist is fine
    }
}
