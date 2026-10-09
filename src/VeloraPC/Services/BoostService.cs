using System.Text;
using VeloraPC.Core;

namespace VeloraPC.Services;

public enum StepState { Pending, Running, Done, Skipped, Failed }
public sealed record BoostEvent(int Step, StepState State, string Detail, double Overall);

public sealed class BoostResult
{
    public long BytesCleaned;
    public long MemoryFreed;
    public int TasksCompleted;
    public int Problems;
    public bool Cancelled;
    public List<(StepState State, string Text)> Lines = new();
    public List<string> Notes = new();
    public StringBuilder Details = new();
}

/// <summary>
/// ULTRA ULTRA FAST PC. Every step does real work, measures real results, and is skipped when not needed.
/// </summary>
public static class BoostService
{
    public static readonly string[] StepTitles =
    {
        "Checking system", "Optimizing memory", "Cleaning temporary files", "Checking Windows health",
        "Applying safe optimizations", "Optimizing gaming settings", "Finalizing"
    };

    const long MinWorthCleaning = 50L * 1024 * 1024;   // don't bother below 50 MB

    public static async Task<BoostResult> RunAsync(IProgress<BoostEvent>? progress, CancellationToken ct)
    {
        var r = new BoostResult();
        int total = StepTitles.Length;
        var started = DateTime.UtcNow;

        void Report(int i, StepState s, string detail, double? frac = null)
        {
            bool finished = s is StepState.Done or StepState.Skipped or StepState.Failed;
            double units = frac.HasValue ? i + frac.Value : i + (finished ? 1.0 : 0.0);
            progress?.Report(new BoostEvent(i, s, detail, Math.Min(1.0, units / total)));
        }

        async Task Step(int i, Func<Task<(StepState State, string Detail, string Did)>> work)
        {
            ct.ThrowIfCancellationRequested();
            Report(i, StepState.Running, "");
            try
            {
                var (state, detail, did) = await work();
                Report(i, state, detail);
                if (state == StepState.Done && i >= 1 && i <= 5) r.TasksCompleted++;
                if (state == StepState.Failed) r.Problems++;
                r.Lines.Add((state, did));
                r.Details.AppendLine($"[{StepTitles[i]}] {state}: {did}");
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Logger.Error($"Boost step '{StepTitles[i]}' failed", ex);
                Report(i, StepState.Failed, "Couldn't complete");
                r.Problems++;
                r.Lines.Add((StepState.Failed, $"{StepTitles[i]} couldn't be completed"));
                r.Details.AppendLine($"[{StepTitles[i]}] Failed: {Friendly.Message(ex)}");
            }
        }

        try
        {
            // 0 — Check system
            await Step(0, () =>
            {
                var m = SystemInfo.Memory();
                var (_, free) = SystemInfo.SystemDrive();
                return Task.FromResult((StepState.Done,
                    $"{Fmt.Gb(m.Used)} / {Fmt.Gb(m.Total)} GB RAM",
                    $"System checked — {Fmt.Bytes(free)} free on the system drive"));
            });

            // 1 — Memory
            await Step(1, async () =>
            {
                var m = SystemInfo.Memory();
                if (m.Percent < 45)
                    return (StepState.Skipped, "Plenty free", "Memory skipped — plenty of memory is already free");
                var res = await MemoryService.BoostAsync(ct);
                r.MemoryFreed = res.Freed;
                return res.Freed > 0
                    ? (StepState.Done, $"{Fmt.Bytes(res.Freed)} freed", $"Memory optimized — {Fmt.Bytes(res.Freed)} freed")
                    : (StepState.Done, "Already balanced", "Memory checked — already well balanced");
            });

            // 2 — Cleanup
            await Step(2, async () =>
            {
                var cats = await Cleanup.ScanAsync(ct);
                long avail = cats.Sum(c => c.Bytes);
                if (avail < MinWorthCleaning)
                    return (StepState.Skipped, $"Only {Fmt.Bytes(avail)}", $"Cleanup skipped — only {Fmt.Bytes(avail)} to clean");

                var prog = new Progress<double>(f =>
                    progress?.Report(new BoostEvent(2, StepState.Running, "Cleaning…", Math.Min(1.0, (2 + f) / total))));
                var res = await Cleanup.CleanAsync(cats, prog, ct);
                r.BytesCleaned = res.Bytes;
                return (StepState.Done, $"{Fmt.Bytes(res.Bytes)} cleaned", $"Temporary files and Recycle Bin cleaned — {Fmt.Bytes(res.Bytes)} removed");
            });

            // 3 — Windows health (fast DISM CheckHealth only; full repairs live in System Repair)
            await Step(3, async () =>
            {
                if (!Admin.IsAdmin)
                    return (StepState.Skipped, "Needs admin", "Windows health check skipped — needs administrator access");
                var last = Config.Current.LastHealthCheckUtc;
                if (last.HasValue && DateTime.UtcNow - last.Value < TimeSpan.FromDays(3))
                    return (StepState.Skipped, "Checked recently", "Windows health check skipped — checked recently");

                var run = await ProcessRunner.RunAsync("dism.exe", "/Online /Cleanup-Image /CheckHealth", ct);
                ct.ThrowIfCancellationRequested();
                r.Details.AppendLine(run.Output);
                var o = run.Output.ToLowerInvariant();

                if (o.Contains("repairable"))
                {
                    Config.Current.LastIntegrityStatus = "Attention";
                    Config.Current.LastIntegrityUtc = DateTime.UtcNow;
                    Config.Current.LastHealthCheckUtc = DateTime.UtcNow;
                    Config.Save();
                    r.Problems++;
                    r.Notes.Add("Windows reported repairable corruption. Open System Repair to fix it.");
                    return (StepState.Done, "Issue found", "Windows health checked — a repairable issue was found");
                }
                if (o.Contains("no component store corruption"))
                {
                    Config.Current.LastIntegrityStatus = "Good";
                    Config.Current.LastIntegrityUtc = DateTime.UtcNow;
                    Config.Current.LastHealthCheckUtc = DateTime.UtcNow;
                    Config.Save();
                    return (StepState.Done, "No problems", "Windows health checked — no problems found");
                }
                if (run.ExitCode != 0)
                    return (StepState.Failed, "Couldn't check", "Windows health check couldn't complete");
                Config.Current.LastHealthCheckUtc = DateTime.UtcNow;
                Config.Save();
                return (StepState.Done, "Finished", "Windows health check finished");
            });

            // 4 — Safe optimizations: fix Power saver while plugged in; list heavy background apps (never closes them)
            await Step(4, async () =>
            {
                var changes = await GameService.ApplyAsync(new GameOptions(false, false, true, false));
                var hogs = GameService.BackgroundApps(3);
                if (hogs.Count > 0)
                    r.Notes.Add("Heavy background apps right now: " +
                        string.Join(", ", hogs.Select(h => $"{h.Name} ({Fmt.Bytes(h.Bytes)})")) + ". Close them if you don't need them.");
                return changes.Count > 0
                    ? (StepState.Done, "Power plan fixed", "Safe optimizations applied — " + string.Join(", ", changes).ToLowerInvariant())
                    : (StepState.Skipped, "Already optimal", "Safe optimizations skipped — settings already optimal");
            });

            // 5 — Gaming
            await Step(5, async () =>
            {
                var changes = await GameService.ApplyAsync(new GameOptions(true, true, false, false));
                return changes.Count > 0
                    ? (StepState.Done, "Applied", "Gaming settings optimized — " + string.Join(", ", changes).ToLowerInvariant())
                    : (StepState.Skipped, "Already optimal", "Gaming settings skipped — already optimized");
            });

            // 6 — Finalize
            await Step(6, () =>
            {
                Config.Current.LastBoostUtc = DateTime.UtcNow;
                Config.Save();
                var summary = $"{Fmt.Bytes(r.BytesCleaned)} cleaned · {Fmt.Bytes(r.MemoryFreed)} memory freed · {r.TasksCompleted} task(s)";
                HistoryStore.Add("boost", "ULTRA ULTRA FAST PC", summary, r.Details.ToString());
                Logger.Info($"Boost finished in {(DateTime.UtcNow - started).TotalSeconds:0.0}s: {summary}");
                return Task.FromResult((StepState.Done, "Finished", "Results saved to History"));
            });
        }
        catch (OperationCanceledException)
        {
            r.Cancelled = true;
            Logger.Info("Boost cancelled by user");
        }
        return r;
    }
}
