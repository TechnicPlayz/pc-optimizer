using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VeloraPC.Core;
using VeloraPC.Services;
using VeloraPC.UI;

namespace VeloraPC.Views;

public sealed class SettingsView : PageView
{
    readonly TextBlock updateStatus = Ui.Text("", 13, false, "MutedBrush");
    readonly StackPanel updateActions = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
    readonly ProgressBar updateBar = Ui.Bar();
    readonly TextBlock updateNotes = Ui.Text("", 12.5, false, "MutedBrush");

    public SettingsView() : base("⚙️ Settings", "Make Velora work the way you like.")
    {
        var s = Config.Current;

        // Appearance
        Section("Appearance", Setting("Theme", "Choose light, dark, or follow Windows.", Segmented(new[] { "System", "Light", "Dark" }, s.Theme, v =>
        {
            s.Theme = v; Config.Save(); App.Shell.RefreshTheme();
        })));

        // General
        Section("General",
            Setting("Start with Windows", "Open Velora when you sign in.", Switch(s.StartWithWindows, on =>
            {
                if (StartupService.SetStartWithWindows(on)) { s.StartWithWindows = on; Config.Save(); return true; }
                App.Shell.Toast("Windows didn't allow that change.", ToastKind.Error); return false;
            })),
            Setting("Notifications", "Show in-app messages when tasks finish.", Switch(s.Notifications, on =>
            {
                s.Notifications = on; Config.Save(); return true;
            })));

        // Maintenance
        Section("Maintenance",
            Setting("Confirm before cleaning", "Ask first before Boost or Deep Clean removes anything.", Switch(s.ConfirmCleanup, on =>
            {
                s.ConfirmCleanup = on; Config.Save(); return true;
            })),
            Setting("Automatic maintenance", "Runs a safe boost every Sunday at 12:00 while you're signed in.", SwitchAsync(s.AutoMaintenance, async on =>
            {
                var ok = await StartupService.SetAutoMaintenanceAsync(on);
                if (ok) { s.AutoMaintenance = on; Config.Save(); return true; }
                App.Shell.Toast("Couldn't change the scheduled task.", ToastKind.Error); return false;
            })));

        // Privacy
        var priv = new StackPanel();
        priv.Children.Add(Ui.Text("Velora collects no telemetry and no personal data.", 14, true));
        var pd = Ui.Text("Nothing about your PC is ever uploaded. The only network request the app makes is the optional update check, " +
                         "which asks GitHub for the latest version number.", 13, false, "MutedBrush");
        pd.Margin = new Thickness(0, 4, 0, 0);
        priv.Children.Add(pd);
        Section("Privacy", priv);

        // Updates
        var upd = new StackPanel();
        upd.Children.Add(Setting("Check automatically", "Look for a new version when Velora starts.", Switch(s.AutoCheckUpdates, on =>
        {
            s.AutoCheckUpdates = on; Config.Save(); return true;
        })));
        var check = Ui.Btn("CHECK FOR UPDATES", () => _ = CheckAsync(), "SecondaryButton", 200);
        check.HorizontalAlignment = HorizontalAlignment.Left;
        check.Margin = new Thickness(0, 12, 0, 0);
        upd.Children.Add(check);
        updateStatus.Margin = new Thickness(0, 10, 0, 0);
        upd.Children.Add(updateStatus);
        updateNotes.Margin = new Thickness(0, 8, 0, 0);
        updateNotes.Visibility = Visibility.Collapsed;
        upd.Children.Add(updateNotes);
        updateBar.Margin = new Thickness(0, 12, 0, 0);
        updateBar.Visibility = Visibility.Collapsed;
        upd.Children.Add(updateBar);
        upd.Children.Add(updateActions);
        Section("Updates", upd);
        if (UpdateService.Pending != null) ShowUpdate(UpdateService.Pending);

        // Logs
        var logs = new StackPanel { Orientation = Orientation.Horizontal };
        var viewLogs = Ui.Btn("VIEW LOGS", OpenLogs, "SecondaryButton", 150); viewLogs.Margin = new Thickness(0, 0, 12, 0);
        logs.Children.Add(viewLogs);
        logs.Children.Add(Ui.Btn("EXPORT LOGS", ExportLogs, "SecondaryButton", 150));
        Section("Logs", logs);

        // About
        var about = new StackPanel();
        about.Children.Add(Ui.Text($"{Branding.Name}  ·  Version {Branding.VersionText}", 15, true));
        var tag = Ui.Text(Branding.Tagline, 13, false, "MutedBrush"); tag.Margin = new Thickness(0, 4, 0, 12);
        about.Children.Add(tag);
        about.Children.Add(Ui.Text("License: End-user license agreement shown during installation (EULA.txt).", 13, false, "MutedBrush"));
        var support = Ui.Btn("GET SUPPORT", () => Open(Branding.SupportUrl), "GhostButton");
        support.HorizontalAlignment = HorizontalAlignment.Left; support.Margin = new Thickness(-18, 8, 0, 0);
        about.Children.Add(support);
        Section("About", about);
    }

    // ---------- builders ----------

    void Section(string title, params UIElement[] content)
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.Text(title, 16, true, "TextBrush", Ui.Display));
        for (int i = 0; i < content.Length; i++)
        {
            content[i].SetValue(MarginProperty, new Thickness(0, 14, 0, 0));
            sp.Children.Add(content[i]);
        }
        var card = Ui.Card(sp, 22, 16);
        card.Margin = new Thickness(0, 0, 0, 14);
        Body.Children.Add(card);
    }

    static Grid Setting(string title, string desc, UIElement control)
    {
        var t = new StackPanel();
        t.Children.Add(Ui.Text(title, 14, true));
        t.Children.Add(Ui.Text(desc, 12.5, false, "MutedBrush"));
        var g = Ui.Row(t, control);
        control.SetValue(MarginProperty, new Thickness(20, 0, 0, 0));
        return g;
    }

    static CheckBox Switch(bool initial, Func<bool, bool> onChange)
        => SwitchAsync(initial, on => Task.FromResult(onChange(on)));

    static CheckBox SwitchAsync(bool initial, Func<bool, Task<bool>> onChange)
    {
        var cb = new CheckBox { IsChecked = initial };
        cb.SetResourceReference(StyleProperty, "SwitchStyle");
        cb.Click += async (_, _) =>
        {
            bool want = cb.IsChecked == true;
            cb.IsEnabled = false;
            bool ok = await onChange(want);
            cb.IsEnabled = true;
            if (!ok) cb.IsChecked = !want;   // revert if it couldn't be applied
        };
        return cb;
    }

    static StackPanel Segmented(string[] options, string selected, Action<string> onChange)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };
        var buttons = new List<(string Name, Button Btn)>();

        void Restyle(string sel)
        {
            foreach (var (name, btn) in buttons)
                btn.SetResourceReference(StyleProperty, name == sel ? "PrimaryButton" : "SecondaryButton");
        }

        foreach (var o in options)
        {
            var name = o;
            var b = Ui.Btn(name, () => { Restyle(name); onChange(name); }, "SecondaryButton", 84);
            b.Margin = new Thickness(0, 0, 6, 0);
            buttons.Add((name, b));
            sp.Children.Add(b);
        }
        Restyle(selected);
        return sp;
    }

    // ---------- actions ----------

    async Task CheckAsync()
    {
        updateStatus.Text = "Checking…";
        Ui.Fg(updateStatus, "MutedBrush");
        updateActions.Children.Clear();
        updateNotes.Visibility = Visibility.Collapsed;
        updateBar.Visibility = Visibility.Collapsed;
        ShowUpdate(await UpdateService.CheckAsync());
    }

    void ShowUpdate(UpdateInfo info)
    {
        updateStatus.Text = info.Message;
        Ui.Fg(updateStatus, info.Available ? "GoodBrush" : "MutedBrush");
        updateActions.Children.Clear();
        updateNotes.Visibility = Visibility.Collapsed;
        if (!info.Available) return;

        if (!string.IsNullOrWhiteSpace(info.Notes))
        {
            updateNotes.Text = "What's new:\n" + info.Notes;
            updateNotes.Visibility = Visibility.Visible;
        }
        if (info.AssetUrl != null)
        {
            var install = Ui.Btn("DOWNLOAD & INSTALL", () => _ = InstallAsync(info), "PrimaryButton", 220);
            install.Margin = new Thickness(0, 0, 10, 0);
            updateActions.Children.Add(install);
        }
        if (info.Url != null)
            updateActions.Children.Add(Ui.Btn("VIEW RELEASE", () => Open(info.Url), "SecondaryButton", 160));
    }

    async Task InstallAsync(UpdateInfo info)
    {
        string size = info.AssetSize.HasValue ? $" ({Fmt.Bytes(info.AssetSize.Value)})" : "";
        bool ok = await App.Shell.Confirm($"Update to Velora {info.Latest}?",
            $"Velora will download the installer{size} from GitHub, close, install the update and reopen. " +
            "Your settings and history are kept.", "Update now", "Not now");
        if (!ok) return;

        updateActions.IsEnabled = false;
        updateBar.Visibility = Visibility.Visible;
        updateBar.BeginAnimation(ProgressBar.ValueProperty, null);
        updateBar.Value = 0;
        updateStatus.Text = "Downloading update…";
        Ui.Fg(updateStatus, "MutedBrush");
        try
        {
            var progress = new Progress<double>(p => Ui.SmoothTo(updateBar, p * 100, 200));
            var path = await UpdateService.DownloadAsync(info, progress, CancellationToken.None);
            updateStatus.Text = "Installing… Velora will reopen in a moment.";
            if (UpdateService.LaunchInstaller(path)) { Application.Current.Shutdown(); return; }
            throw new InvalidOperationException("The installer couldn't be started.");
        }
        catch (Exception ex)
        {
            Logger.Error("Update failed", ex);
            updateStatus.Text = "We couldn't complete the update. " + (ex is InvalidDataException
                ? "The download didn't pass its integrity check, so it was discarded."
                : Friendly.Message(ex));
            Ui.Fg(updateStatus, "BadBrush");
            updateBar.Visibility = Visibility.Collapsed;
            updateActions.IsEnabled = true;
        }
    }

    static void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Error("Could not open " + target, ex); }
    }

    static void OpenLogs()
    {
        try
        {
            Directory.CreateDirectory(Paths.Logs);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Paths.Logs}\"") { UseShellExecute = true });
        }
        catch (Exception ex) { Logger.Error("Could not open logs folder", ex); }
    }

    static void ExportLogs()
    {
        var dlg = new SaveFileDialog { FileName = "velora-logs.txt", Filter = "Text file (*.txt)|*.txt" };
        if (dlg.ShowDialog() != true) return;
        try { Logger.Export(dlg.FileName); App.Shell.Toast("Logs exported.", ToastKind.Success); }
        catch (Exception ex) { Logger.Error("Log export failed", ex); App.Shell.Toast(Friendly.Message(ex), ToastKind.Error); }
    }
}
