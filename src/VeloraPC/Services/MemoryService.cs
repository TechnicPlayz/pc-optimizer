using System.Diagnostics;
using VeloraPC.Core;

namespace VeloraPC.Services;

public sealed record MemoryBoostResult(ulong UsedBefore, ulong UsedAfter, ulong AvailBefore, ulong AvailAfter, int Trimmed, long Freed);

/// <summary>
/// Asks Windows (documented EmptyWorkingSet API) to trim memory that user apps hold but aren't actively using.
/// Nothing is killed. System processes and the foreground app are skipped. All numbers are measured, not estimated.
/// </summary>
public static class MemoryService
{
    static readonly HashSet<string> Skip = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "MemCompression", "Memory Compression", "smss", "csrss", "wininit",
        "services", "lsass", "winlogon", "dwm", "fontdrvhost", "audiodg", "svchost", "MsMpEng",
        "SecurityHealthService", "VeloraPC"
    };

    public static Task<MemoryBoostResult> BoostAsync(CancellationToken ct, IProgress<TaskProgress>? progress = null) => Task.Run(async () =>
    {
        var before = SystemInfo.Memory();
        progress?.Report(new TaskProgress(0, "Looking at running apps…"));

        uint fgPid;
        Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out fgPid);
        int mySession = Process.GetCurrentProcess().SessionId;
        var procs = Process.GetProcesses();
        int trimmed = 0;

        try
        {
            for (int i = 0; i < procs.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var p = procs[i];
                try
                {
                    if (p.Id <= 4 || (uint)p.Id == fgPid || p.SessionId != mySession) continue;
                    var name = p.ProcessName;
                    if (Skip.Contains(name)) continue;

                    progress?.Report(new TaskProgress(0.9 * i / procs.Length, $"Trimming {name}…"));
                    IntPtr h = Native.OpenProcess(Native.PROCESS_SET_QUOTA | Native.PROCESS_QUERY_INFORMATION, false, p.Id);
                    if (h == IntPtr.Zero) continue;
                    try { if (Native.EmptyWorkingSet(h)) trimmed++; }
                    finally { Native.CloseHandle(h); }
                }
                catch { /* process exited or access denied: skip it */ }
            }
        }
        finally
        {
            foreach (var q in procs) q.Dispose();
        }

        // Let Windows settle so the "after" reading is real.
        for (int k = 1; k <= 3; k++)
        {
            progress?.Report(new TaskProgress(0.9 + 0.1 * k / 3, "Measuring results…"));
            await Task.Delay(500, ct);
        }

        var after = SystemInfo.Memory();
        long freed = (long)after.Avail - (long)before.Avail;
        Logger.Info($"Memory boost: trimmed {trimmed} processes, available {Fmt.Bytes(before.Avail)} -> {Fmt.Bytes(after.Avail)}");
        progress?.Report(new TaskProgress(1.0, "Done"));
        return new MemoryBoostResult(before.Used, after.Used, before.Avail, after.Avail, trimmed, Math.Max(0, freed));
    }, ct);
}
