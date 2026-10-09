using VeloraPC.Core;

namespace VeloraPC.Services;

public enum HealthStatus { Good, Attention, Warning, Unknown }

public sealed record HealthCheck(string Name, string Emoji, HealthStatus Status, string Detail);

/// <summary>Read-only diagnostics. Never invents problems: thresholds are conservative.</summary>
public static class HealthService
{
    public static List<HealthCheck> Evaluate()
    {
        var list = new List<HealthCheck>();

        // Storage
        var (total, free) = SystemInfo.SystemDrive();
        if (total > 0)
        {
            double pct = free * 100.0 / total;
            var st = pct >= 15 ? HealthStatus.Good : pct >= 7 ? HealthStatus.Attention : HealthStatus.Warning;
            list.Add(new("Storage", "💾", st, $"{Fmt.Bytes(free)} free of {Fmt.Bytes(total)} ({pct:0}% free)"));
        }

        // Memory
        var m = SystemInfo.Memory();
        if (m.Total > 0)
        {
            var st = m.Percent < 85 ? HealthStatus.Good : m.Percent < 93 ? HealthStatus.Attention : HealthStatus.Warning;
            list.Add(new("Memory", "🧠", st, $"{Fmt.Gb(m.Used)} of {Fmt.Gb(m.Total)} GB in use ({m.Percent:0}%)"));
        }

        // Uptime
        var up = SystemInfo.Uptime;
        list.Add(new("System uptime", "⏱", up.TotalDays <= 14 ? HealthStatus.Good : HealthStatus.Attention,
            up.TotalDays < 1 ? $"Running for {up.Hours}h {up.Minutes}m"
                             : $"Running for {(int)up.TotalDays} day(s){(up.TotalDays > 14 ? " — a restart can help" : "")}"));

        // Windows Update restart state
        bool pending = SystemInfo.PendingReboot();
        list.Add(new("Windows Update", "🔄", pending ? HealthStatus.Attention : HealthStatus.Good,
            pending ? "A restart is needed to finish installing updates" : "No restart pending"));

        // Startup apps
        int startup = SystemInfo.StartupAppCount();
        list.Add(new("Startup applications", "🚀", startup <= 12 ? HealthStatus.Good : HealthStatus.Attention,
            startup <= 12 ? $"{startup} item(s) start with Windows"
                          : $"{startup} items start with Windows — review them in Task Manager > Startup apps"));

        // Network
        bool net = SystemInfo.NetworkAvailable();
        list.Add(new("Network", "🌐", net ? HealthStatus.Good : HealthStatus.Attention,
            net ? "Connected" : "No network connection detected"));

        // Windows integrity (from the last real check only)
        var s = Config.Current;
        if (s.LastIntegrityStatus == null)
            list.Add(new("Windows integrity", "🛡", HealthStatus.Unknown, "Not checked yet — run a check in System Repair"));
        else if (s.LastIntegrityStatus == "Good")
            list.Add(new("Windows integrity", "🛡", HealthStatus.Good, $"No problems found ({Fmt.When(s.LastIntegrityUtc)})"));
        else
            list.Add(new("Windows integrity", "🛡", HealthStatus.Attention, $"Repairable issues were reported ({Fmt.When(s.LastIntegrityUtc)}) — open System Repair"));

        return list;
    }

    public static HealthStatus Overall(IEnumerable<HealthCheck> checks)
    {
        var l = checks.ToList();
        if (l.Any(c => c.Status == HealthStatus.Warning)) return HealthStatus.Warning;
        if (l.Any(c => c.Status == HealthStatus.Attention)) return HealthStatus.Attention;
        return HealthStatus.Good;
    }

    public static string Label(HealthStatus s) => s switch
    {
        HealthStatus.Good => "GOOD",
        HealthStatus.Attention => "ATTENTION",
        HealthStatus.Warning => "WARNING",
        _ => "NOT CHECKED"
    };

    public static string BrushKey(HealthStatus s) => s switch
    {
        HealthStatus.Good => "GoodBrush",
        HealthStatus.Attention => "WarnBrush",
        HealthStatus.Warning => "BadBrush",
        _ => "MutedBrush"
    };
}
