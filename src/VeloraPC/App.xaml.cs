using System.Windows;
using VeloraPC.Core;
using VeloraPC.Services;
using VeloraPC.UI;

namespace VeloraPC;

public partial class App : Application
{
    public static IShell Shell { get; private set; } = null!;
    public static string[] StartupArgs { get; private set; } = Array.Empty<string>();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        StartupArgs = e.Args;
        Logger.Prune();
        Logger.Info($"{Branding.Name} {Branding.VersionText} starting (admin={Admin.IsAdmin}, args=[{string.Join(' ', e.Args)}])");

        DispatcherUnhandledException += (_, a) =>
        {
            Logger.Error("Unhandled UI exception", a.Exception);
            a.Handled = true;   // never crash because a Windows command failed
            try { Shell?.Toast("Something went wrong. Details were saved to the log.", ToastKind.Error); } catch { }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, a) => Logger.Error("Fatal error", a.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, a) => { Logger.Error("Background task error", a.Exception); a.SetObserved(); };

        // Headless scheduled maintenance: no window, just the safe boost, then exit.
        if (e.Args.Contains("--auto"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try { await BoostService.RunAsync(null, CancellationToken.None); }
            catch (Exception ex) { Logger.Error("Scheduled maintenance failed", ex); }
            Shutdown();
            return;
        }

        ThemeService.Apply(Config.Current.Theme);
        var window = new MainWindow();
        MainWindow = window;
        Shell = window;
        window.Show();
        window.Start();
    }
}
