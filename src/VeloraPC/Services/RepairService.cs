using System.Text;
using System.Text.RegularExpressions;
using VeloraPC.Core;

namespace VeloraPC.Services;

public enum RepairKind { Sfc, DismCheck, DismScan, DismRestore }
public enum RepairOutcome { Ok, Attention, Failed, Cancelled }
public sealed record RepairResult(RepairKind Kind, RepairOutcome Outcome, string Summary, string Output);

public static class RepairService
{
    static readonly Regex Percent = new(@"(\d{1,3}(?:\.\d+)?)\s*%", RegexOptions.Compiled);

    public static string Title(RepairKind k) => k switch
    {
        RepairKind.Sfc => "System File Checker (SFC)",
        RepairKind.DismCheck => "DISM — Check Health",
        RepairKind.DismScan => "DISM — Scan Health",
        _ => "DISM — Restore Health"
    };

    public static string Explain(RepairKind k) => k switch
    {
        RepairKind.Sfc => "Scans protected Windows files and replaces any that are damaged. Usually 5–15 minutes.",
        RepairKind.DismCheck => "A quick check of whether Windows has already flagged corruption. Takes a few seconds and changes nothing.",
        RepairKind.DismScan => "A deeper scan of the Windows component store. Takes several minutes and changes nothing.",
        _ => "Repairs the Windows component store using Windows Update. Needs internet; can take 10–30 minutes."
    };

    static (string File, string Args, Encoding Enc) Command(RepairKind k) => k switch
    {
        RepairKind.Sfc => ("sfc.exe", "/scannow", Encoding.Unicode),
        RepairKind.DismCheck => ("dism.exe", "/Online /Cleanup-Image /CheckHealth", Encoding.UTF8),
        RepairKind.DismScan => ("dism.exe", "/Online /Cleanup-Image /ScanHealth", Encoding.UTF8),
        _ => ("dism.exe", "/Online /Cleanup-Image /RestoreHealth", Encoding.UTF8)
    };

    public static async Task<RepairResult> RunAsync(RepairKind kind, IProgress<double>? percent, CancellationToken ct)
    {
        var (file, args, enc) = Command(kind);
        var run = await ProcessRunner.RunAsync(file, args, ct, line =>
        {
            var m = Percent.Match(line);
            if (m.Success && double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v))
                percent?.Report(Math.Clamp(v, 0, 100));
        }, enc);

        if (run.Cancelled) return new(kind, RepairOutcome.Cancelled, "Cancelled.", run.Output);
        if (run.Error != null) return new(kind, RepairOutcome.Failed, "Windows couldn't start this tool.", run.Output);

        var o = run.Output.ToLowerInvariant();
        RepairOutcome outcome; string summary;

        switch (kind)
        {
            case RepairKind.Sfc:
                if (o.Contains("did not find any integrity violations")) { outcome = RepairOutcome.Ok; summary = "No integrity violations found."; }
                else if (o.Contains("successfully repaired")) { outcome = RepairOutcome.Ok; summary = "Damaged files were found and repaired."; }
                else if (o.Contains("unable to fix")) { outcome = RepairOutcome.Attention; summary = "Some files couldn't be repaired. Run DISM Restore Health, then SFC again."; }
                else if (run.ExitCode == 0) { outcome = RepairOutcome.Ok; summary = "Finished. See details for the full report."; }
                else { outcome = RepairOutcome.Failed; summary = "SFC couldn't complete. Make sure the app is running as administrator."; }
                break;

            case RepairKind.DismRestore:
                if (o.Contains("completed successfully")) { outcome = RepairOutcome.Ok; summary = "The component store was repaired."; }
                else if (run.ExitCode == 0) { outcome = RepairOutcome.Ok; summary = "Finished. See details for the full report."; }
                else { outcome = RepairOutcome.Failed; summary = "Repair couldn't complete. Check your internet connection and try again."; }
                break;

            default:   // CheckHealth / ScanHealth
                if (o.Contains("no component store corruption")) { outcome = RepairOutcome.Ok; summary = "No corruption detected."; }
                else if (o.Contains("repairable")) { outcome = RepairOutcome.Attention; summary = "Corruption was detected and is repairable. Run Restore Health."; }
                else if (run.ExitCode == 0) { outcome = RepairOutcome.Ok; summary = "Finished. See details for the full report."; }
                else { outcome = RepairOutcome.Failed; summary = "The check couldn't complete. Make sure the app is running as administrator."; }
                break;
        }

        if (kind != RepairKind.DismRestore && outcome != RepairOutcome.Failed)
        {
            Config.Current.LastIntegrityStatus = outcome == RepairOutcome.Ok ? "Good" : "Attention";
            Config.Current.LastIntegrityUtc = DateTime.UtcNow;
            if (kind != RepairKind.Sfc) Config.Current.LastHealthCheckUtc = DateTime.UtcNow;
            Config.Save();
        }
        return new(kind, outcome, summary, run.Output);
    }
}
