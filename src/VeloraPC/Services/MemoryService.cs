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

    public static Task<MemoryBoostResult> BoostAsync(CancellationToken ct) => Task.Run(async () =>
    {
        var before = SystemInfo.Memory();
        int trimmed = 0;

        uint fgPid = 0;
        Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out fgPid);
        int mySession = Process.GetCurrentProcess().SessionId;

        foreach (var p in Process.GetProcesses())
        {
            using (p)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (p.Id <= 4 || (uint)p.Id == fgPid || p.SessionId != mySession || Skip.Contains(p.ProcessName)) continue;
                    IntPtr h = Native.OpenProcess(Native.PROCESS_SET_QUOTA | Native.PROCESS_QUERY_INFORMATION, false, p.Id);
                    if (h == IntPtr.Zero) continue;
                    try { if (Native.EmptyWorkingSet(h)) trimmed++; }
                    finally { Native.CloseHandle(h); }
                }
                catch { /* process exited or access denied: skip */ }
            }
        }

        await Task.Delay(1500, ct);   // let Windows settle so the "after" reading is real
        var after = SystemInfo.Memory();
        long freed = (long)after.Avail - (long)before.Avail;
        Logger.Info($"Memory boost: trimmed {trimmed} processes, available {Fmt.Bytes(before.Avail)} -> {Fmt.Bytes(after.Avail)}");
        return new MemoryBoostResult(before.Used, after.Used, before.Avail, after.Avail, trimmed, Math.Max(0, freed));
    }, ct);
}
