using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace VeloraPC.Core;

public static class Branding
{
    public const string Name = "Velora PC";
    public const string Tagline = "Keep your PC clean, healthy and optimized.";
    public const string Repo = "TechnicPlayz/pc-optimizer";
    public static string SupportUrl => $"https://github.com/{Repo}/issues";
    public static string ReleasesUrl => $"https://github.com/{Repo}/releases";
    public static System.Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new System.Version(1, 0, 0);
    public static string VersionText => $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{Math.Max(0, CurrentVersion.Build)}";
}

public static class Paths
{
    public static readonly string Data = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VeloraPC");
    public static string Logs => Path.Combine(Data, "logs");
    public static string SettingsFile => Path.Combine(Data, "settings.json");
    public static string HistoryFile => Path.Combine(Data, "history.json");
}

public static class Logger
{
    static readonly object Gate = new();
    static string FilePath => Path.Combine(Paths.Logs, $"velora-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string m) => Write("INFO", m);
    public static void Warn(string m) => Write("WARN", m);
    public static void Error(string m, Exception? ex = null) => Write("ERROR", ex == null ? m : $"{m}: {ex}");

    static void Write(string level, string m)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Paths.Logs);
                File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss.fff} [{level}] {m}{Environment.NewLine}");
            }
        }
        catch { /* logging must never crash the app */ }
    }

    public static void Prune()
    {
        try
        {
            if (!Directory.Exists(Paths.Logs)) return;
            foreach (var f in Directory.GetFiles(Paths.Logs, "*.log"))
                if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-14)) File.Delete(f);
        }
        catch { }
    }

    public static void Export(string destination)
    {
        var sb = new StringBuilder();
        if (Directory.Exists(Paths.Logs))
        {
            foreach (var f in Directory.GetFiles(Paths.Logs, "*.log").OrderBy(x => x))
            {
                sb.AppendLine($"===== {Path.GetFileName(f)} =====");
                sb.AppendLine(File.ReadAllText(f));
            }
        }
        if (sb.Length == 0) sb.AppendLine("No log entries yet.");
        File.WriteAllText(destination, sb.ToString());
    }
}

// ---------------- Settings ----------------

public sealed class GameSnapshot
{
    public int? GameMode { get; set; }
    public int? AppCapture { get; set; }
    public int? GameDvr { get; set; }
    public string? PowerScheme { get; set; }
}

public sealed class Settings
{
    public string Theme { get; set; } = "System";           // System | Light | Dark
    public bool StartWithWindows { get; set; }
    public bool Notifications { get; set; } = true;          // in-app notifications
    public bool ConfirmCleanup { get; set; }                 // ask before cleaning / boosting
    public bool AutoMaintenance { get; set; }
    public bool AutoCheckUpdates { get; set; } = true;
    public bool OnboardingDone { get; set; }
    public DateTime? LastBoostUtc { get; set; }
    public DateTime? LastHealthCheckUtc { get; set; }
    public DateTime? LastIntegrityUtc { get; set; }
    public string? LastIntegrityStatus { get; set; }         // Good | Attention
    public GameSnapshot? GameSnapshot { get; set; }
}

public static class Config
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static Settings Current { get; } = Load();

    static Settings Load()
    {
        try
        {
            if (File.Exists(Paths.SettingsFile))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Paths.SettingsFile)) ?? new Settings();
        }
        catch (Exception ex) { Logger.Error("Could not read settings", ex); }
        return new Settings();
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Paths.Data);
            File.WriteAllText(Paths.SettingsFile, JsonSerializer.Serialize(Current, Json));
        }
        catch (Exception ex) { Logger.Error("Could not save settings", ex); }
    }
}

// ---------------- History ----------------

public sealed class HistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime TimeUtc { get; set; } = DateTime.UtcNow;
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Details { get; set; } = "";
}

public static class HistoryStore
{
    static readonly object Gate = new();
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    static List<HistoryEntry> items = Load();

    static List<HistoryEntry> Load()
    {
        try
        {
            if (File.Exists(Paths.HistoryFile))
                return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(Paths.HistoryFile)) ?? new();
        }
        catch (Exception ex) { Logger.Error("Could not read history", ex); }
        return new List<HistoryEntry>();
    }

    public static void Add(string kind, string title, string summary, string details = "")
    {
        lock (Gate)
        {
            items.Add(new HistoryEntry { Kind = kind, Title = title, Summary = summary, Details = details });
            if (items.Count > 200) items = items.OrderByDescending(i => i.TimeUtc).Take(200).ToList();
            try
            {
                Directory.CreateDirectory(Paths.Data);
                File.WriteAllText(Paths.HistoryFile, JsonSerializer.Serialize(items, Json));
            }
            catch (Exception ex) { Logger.Error("Could not save history", ex); }
        }
    }

    public static List<HistoryEntry> All()
    {
        lock (Gate) return items.OrderByDescending(i => i.TimeUtc).ToList();
    }

    public static void Clear()
    {
        lock (Gate)
        {
            items = new List<HistoryEntry>();
            try { if (File.Exists(Paths.HistoryFile)) File.Delete(Paths.HistoryFile); } catch { }
        }
    }
}

// ---------------- Admin ----------------

public static class Admin
{
    public static bool IsAdmin
    {
        get
        {
            try
            {
                using var id = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }

    /// <summary>Restarts the app elevated (one UAC prompt). Returns false if the user declined.</summary>
    public static bool Relaunch(string? args = null)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                Arguments = args ?? "",
                UseShellExecute = true,
                Verb = "runas"
            });
            return true;
        }
        catch (Win32Exception) { return false; }   // user cancelled UAC
        catch (Exception ex) { Logger.Error("Relaunch failed", ex); return false; }
    }
}

// ---------------- Process runner (all Windows commands go through here) ----------------

public sealed record RunResult(int ExitCode, string Output, bool Cancelled, string? Error);

public static class ProcessRunner
{
    public static async Task<RunResult> RunAsync(string file, string args, CancellationToken ct = default,
        Action<string>? onLine = null, Encoding? encoding = null)
    {
        var sb = new StringBuilder();
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding ?? Encoding.UTF8,
            StandardErrorEncoding = encoding ?? Encoding.UTF8
        };

        try
        {
            using var p = new Process { StartInfo = psi };
            DataReceivedEventHandler handler = (_, e) =>
            {
                if (e.Data == null) return;
                var line = e.Data.Replace("\0", "").Trim();
                if (line.Length == 0) return;
                lock (sb) sb.AppendLine(line);
                try { onLine?.Invoke(line); } catch { }
            };
            p.OutputDataReceived += handler;
            p.ErrorDataReceived += handler;

            Logger.Info($"RUN {file} {args}");
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            using var reg = ct.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } });
            await p.WaitForExitAsync(CancellationToken.None);

            string output;
            lock (sb) output = sb.ToString();
            Logger.Info($"EXIT {file} code={p.ExitCode}");
            return new RunResult(p.ExitCode, output, ct.IsCancellationRequested, null);
        }
        catch (Exception ex)
        {
            Logger.Error($"Could not run {file}", ex);
            return new RunResult(-1, "", false, ex.Message);
        }
    }
}

// ---------------- Formatting & friendly errors ----------------

public static class Fmt
{
    public static string Bytes(double b)
    {
        if (b < 0) b = 0;
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
        return i <= 1 ? $"{b:0} {u[i]}" : $"{b:0.0} {u[i]}";
    }

    public static string Gb(double bytes) => $"{bytes / 1073741824.0:0.0}";

    public static string Greeting()
    {
        int h = DateTime.Now.Hour;
        return h < 12 ? "Good morning" : h < 18 ? "Good afternoon" : "Good evening";
    }

    public static string When(DateTime? utc)
    {
        if (utc == null) return "Not yet";
        var l = utc.Value.ToLocalTime();
        if (l.Date == DateTime.Today) return $"Today, {l:HH:mm}";
        if (l.Date == DateTime.Today.AddDays(-1)) return $"Yesterday, {l:HH:mm}";
        return l.ToString("dd MMM yyyy, HH:mm");
    }

    public static string Day(DateTime utc)
    {
        var l = utc.ToLocalTime().Date;
        if (l == DateTime.Today) return "Today";
        if (l == DateTime.Today.AddDays(-1)) return "Yesterday";
        return l.ToString("dddd, dd MMM yyyy");
    }
}

public static class Friendly
{
    public static string Message(Exception ex) => ex switch
    {
        UnauthorizedAccessException => "Windows denied permission. Try running the app as administrator.",
        IOException => "A file was in use. Close other apps and try again.",
        Win32Exception => "Windows couldn't start a required tool.",
        OperationCanceledException => "The operation was cancelled.",
        _ => "Something unexpected happened. Details were saved to the log."
    };
}

// ---------------- Free / Pro structure (everything unlocked for now) ----------------

public enum Feature
{
    UltraBoost, AdvancedCleanup, AdvancedDiagnostics, GameBoost,
    ScheduledMaintenance, DetailedReports, MaintenanceHistory, AdvancedRepair
}

public static class Edition
{
    /// <summary>Replace with real license validation when Pro launches.</summary>
    public static bool IsPro => true;

    static readonly HashSet<Feature> FreeFeatures = new() { Feature.MaintenanceHistory };

    public static bool Has(Feature f) => IsPro || FreeFeatures.Contains(f);
}
