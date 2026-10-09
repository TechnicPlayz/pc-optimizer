using Microsoft.Win32;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace VeloraPC.Core;

public readonly record struct MemInfo(ulong Total, ulong Avail)
{
    public ulong Used => Total > Avail ? Total - Avail : 0;
    public double Percent => Total == 0 ? 0 : Used * 100.0 / Total;
}

public static class SystemInfo
{
    static ulong prevIdle, prevTotal;

    static SystemInfo() { CpuPercent(); }   // establish a CPU baseline

    public static MemInfo Memory()
    {
        var m = new Native.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
        return Native.GlobalMemoryStatusEx(ref m) ? new MemInfo(m.ullTotalPhys, m.ullAvailPhys) : new MemInfo(0, 0);
    }

    static ulong ToUlong(FILETIME f) => ((ulong)(uint)f.dwHighDateTime << 32) | (uint)f.dwLowDateTime;

    public static double CpuPercent()
    {
        if (!Native.GetSystemTimes(out var idleFt, out var kernelFt, out var userFt)) return 0;
        ulong idle = ToUlong(idleFt);
        ulong total = ToUlong(kernelFt) + ToUlong(userFt);   // kernel time already includes idle
        ulong dIdle = idle - prevIdle, dTotal = total - prevTotal;
        prevIdle = idle; prevTotal = total;
        if (dTotal == 0) return 0;
        return Math.Clamp((1.0 - (double)dIdle / dTotal) * 100.0, 0, 100);
    }

    public static (long Total, long Free) SystemDrive()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var d = new DriveInfo(root);
            return (d.TotalSize, d.AvailableFreeSpace);
        }
        catch { return (0, 0); }
    }

    public static TimeSpan Uptime => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public static (bool OnAc, bool HasBattery) Power()
    {
        if (!Native.GetSystemPowerStatus(out var s)) return (true, false);
        bool hasBattery = s.BatteryFlag != 128 && s.BatteryFlag != 255;
        return (s.ACLineStatus == 1 || !hasBattery, hasBattery);
    }

    public static int StartupAppCount()
    {
        int n = 0;
        try
        {
            string run = @"Software\Microsoft\Windows\CurrentVersion\Run";
            using (var k = Registry.CurrentUser.OpenSubKey(run)) n += k?.ValueCount ?? 0;
            using (var k = Registry.LocalMachine.OpenSubKey(run)) n += k?.ValueCount ?? 0;
            using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run")) n += k?.ValueCount ?? 0;
            foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                                           Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup) })
            {
                if (Directory.Exists(folder))
                    n += Directory.GetFiles(folder).Count(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase));
            }
        }
        catch { }
        return n;
    }

    public static bool PendingReboot()
    {
        try
        {
            using var a = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            if (a != null) return true;
            using var b = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            return b != null;
        }
        catch { return false; }
    }

    public static bool NetworkAvailable() => NetworkInterface.GetIsNetworkAvailable();
}
