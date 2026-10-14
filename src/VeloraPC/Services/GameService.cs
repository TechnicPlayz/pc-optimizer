using Microsoft.Win32;
using System.Diagnostics;
using System.Text.RegularExpressions;
using VeloraPC.Core;

namespace VeloraPC.Services;

public sealed class GameStatus
{
    public bool GameMode;
    public bool Capture;
    public string SchemeId = "";
    public string SchemeName = "Unknown";
    public bool OnAc = true;
    public bool HasBattery;
}

public sealed record GameOptions(bool GameMode, bool CaptureOff, bool FixPowerSaver, bool HighPerformance);
public sealed record BackgroundApp(string Name, long Bytes);

/// <summary>Documented per-user Windows gaming settings. Original values are saved so everything is reversible.</summary>
public static class GameService
{
    public const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    public const string HighPerf = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    public const string PowerSaver = "a1841308-3541-4fab-bc81-f71556f20b4a";

    const string GameBarKey = @"Software\Microsoft\GameBar";
    const string DvrKey = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
    const string StoreKey = @"System\GameConfigStore";

    static int? GetDword(string path, string name)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(path);
            return k?.GetValue(name) is int i ? i : null;
        }
        catch { return null; }
    }

    static bool SetDword(string path, string name, int value)
    {
        try
        {
            using var k = Registry.CurrentUser.CreateSubKey(path);
            k.SetValue(name, value, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception ex) { Logger.Error($"Registry write failed ({name})", ex); return false; }
    }

    static void Restore(string path, string name, int? original)
    {
        try
        {
            if (original.HasValue) { SetDword(path, name, original.Value); return; }
            using var k = Registry.CurrentUser.OpenSubKey(path, true);
            k?.DeleteValue(name, false);
        }
        catch (Exception ex) { Logger.Error($"Registry restore failed ({name})", ex); }
    }

    static async Task<(string Id, string Name)> ActiveSchemeAsync()
    {
        var r = await ProcessRunner.RunAsync("powercfg.exe", "/getactivescheme");
        var m = Regex.Match(r.Output, @"[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}");
        if (!m.Success) return ("", "Unknown");
        var id = m.Value.ToLowerInvariant();
        string name = id switch
        {
            Balanced => "Balanced",
            HighPerf => "High performance",
            PowerSaver => "Power saver",
            _ => Regex.Match(r.Output, @"\(([^)]+)\)").Groups[1].Value
        };
        return (id, string.IsNullOrWhiteSpace(name) ? "Custom" : name);
    }

    static async Task<bool> SetSchemeAsync(string id)
    {
        var r = await ProcessRunner.RunAsync("powercfg.exe", $"/setactive {id}");
        return r.ExitCode == 0;
    }

    public static async Task<GameStatus> GetStatusAsync()
    {
        var (acOn, hasBattery) = SystemInfo.Power();
        var (id, name) = await ActiveSchemeAsync();
        return new GameStatus
        {
            GameMode = (GetDword(GameBarKey, "AutoGameModeEnabled") ?? 1) == 1,
            Capture = (GetDword(DvrKey, "AppCaptureEnabled") ?? 1) == 1 || (GetDword(StoreKey, "GameDVR_Enabled") ?? 1) == 1,
            SchemeId = id, SchemeName = name, OnAc = acOn, HasBattery = hasBattery
        };
    }

    static void EnsureSnapshot(GameStatus st)
    {
        if (Config.Current.GameSnapshot != null) return;
        Config.Current.GameSnapshot = new GameSnapshot
        {
            GameMode = GetDword(GameBarKey, "AutoGameModeEnabled"),
            AppCapture = GetDword(DvrKey, "AppCaptureEnabled"),
            GameDvr = GetDword(StoreKey, "GameDVR_Enabled"),
            PowerScheme = st.SchemeId
        };
        Config.Save();
    }

    /// <summary>Applies only what is needed. Returns human-readable list of what actually changed.</summary>
    public static async Task<List<string>> ApplyAsync(GameOptions o)
    {
        var changes = new List<string>();
        var st = await GetStatusAsync();
        EnsureSnapshot(st);

        if (o.GameMode && !st.GameMode && SetDword(GameBarKey, "AutoGameModeEnabled", 1))
            changes.Add("Game Mode turned on");

        if (o.CaptureOff && st.Capture)
        {
            bool a = SetDword(DvrKey, "AppCaptureEnabled", 0);
            bool b = SetDword(StoreKey, "GameDVR_Enabled", 0);
            if (a || b) changes.Add("Background capture turned off");
        }

        bool plugged = st.OnAc || !st.HasBattery;
        if (o.HighPerformance && plugged && st.SchemeId != HighPerf)
        {
            if (await SetSchemeAsync(HighPerf)) changes.Add("Power plan set to High performance");
        }
        else if (o.FixPowerSaver && plugged && st.SchemeId == PowerSaver)
        {
            if (await SetSchemeAsync(Balanced)) changes.Add("Power plan changed from Power saver to Balanced");
        }

        if (changes.Count > 0) Logger.Info("Game settings applied: " + string.Join("; ", changes));
        return changes;
    }

    public static bool HasSnapshot => Config.Current.GameSnapshot != null;

    public static async Task<bool> RevertAsync()
    {
        var s = Config.Current.GameSnapshot;
        if (s == null) return false;
        Restore(GameBarKey, "AutoGameModeEnabled", s.GameMode);
        Restore(DvrKey, "AppCaptureEnabled", s.AppCapture);
        Restore(StoreKey, "GameDVR_Enabled", s.GameDvr);
        if (!string.IsNullOrEmpty(s.PowerScheme)) await SetSchemeAsync(s.PowerScheme);
        Config.Current.GameSnapshot = null;
        Config.Save();
        Logger.Info("Game settings reverted to original");
        return true;
    }

    static readonly HashSet<string> Ignore = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "MemCompression", "svchost", "dwm", "explorer", "csrss", "lsass",
        "MsMpEng", "winlogon", "services", "fontdrvhost", "VeloraPC", "SearchHost", "ShellExperienceHost"
    };

    /// <summary>Recommendation only: the biggest background apps. Nothing is closed automatically.</summary>
    public static List<BackgroundApp> BackgroundApps(int take = 5)
    {
        var list = new List<BackgroundApp>();
        try
        {
            uint fg = 0;
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out fg);
            int session = Process.GetCurrentProcess().SessionId;
            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    try
                    {
                        if (p.SessionId != session || (uint)p.Id == fg || Ignore.Contains(p.ProcessName)) continue;
                        if (p.WorkingSet64 > 300L * 1024 * 1024) list.Add(new BackgroundApp(p.ProcessName, p.WorkingSet64));
                    }
                    catch { }
                }
            }
        }
        catch { }
        return list.GroupBy(a => a.Name).Select(g => new BackgroundApp(g.Key, g.Sum(x => x.Bytes)))
                   .OrderByDescending(a => a.Bytes).Take(take).ToList();
    }
}
