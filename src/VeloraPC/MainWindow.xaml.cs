using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using VeloraPC.Core;
using VeloraPC.Services;
using VeloraPC.UI;
using VeloraPC.Views;

namespace VeloraPC;

public partial class MainWindow : Window, IShell
{
    static readonly (string Key, string Emoji, string Label)[] NavItems =
    {
        ("dashboard", "🏠", "Dashboard"),
        ("ram",       "⚡", "RAM Boost"),
        ("clean",     "🧹", "Deep Clean"),
        ("game",      "🎮", "Game Boost"),
        ("health",    "🩺", "PC Health"),
        ("repair",    "🔧", "System Repair"),
        ("history",   "🕘", "History"),
        ("settings",  "⚙️", "Settings"),
    };

    readonly Dictionary<string, Button> navButtons = new();
    PageView? current;
    string currentKey = "dashboard";

    public MainWindow()
    {
        InitializeComponent();
        BuildSidebar();
        SourceInitialized += (_, _) => UpdateTitleBar();
    }

    public void Start()
    {
        var page = App.StartupArgs.FirstOrDefault(a => a.StartsWith("--page="))?.Substring(7) ?? "dashboard";
        Navigate(page);

        if (!Config.Current.OnboardingDone) ShowOnboarding();
        else if (Config.Current.AutoCheckUpdates) _ = CheckUpdatesQuietlyAsync();
    }

    // ---------- Sidebar ----------

    void BuildSidebar()
    {
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0) };
        var logo = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(10) };
        logo.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
        var bolt = Ui.Icon("⚡", 18); bolt.HorizontalAlignment = HorizontalAlignment.Center;
        logo.Child = bolt;
        brand.Children.Add(logo);
        var names = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(Ui.Text("Velora", 19, true, "TextBrush", Ui.Display));
        var sub = Ui.Text("PC", 11, true, "MutedBrush"); sub.Margin = new Thickness(0, -2, 0, 0);
        names.Children.Add(sub);
        brand.Children.Add(names);
        BrandPanel.Children.Add(brand);

        foreach (var (key, emoji, label) in NavItems)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var ic = Ui.Icon(emoji, 16); ic.Width = 26;
            row.Children.Add(ic);
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });

            var b = new Button { Content = row, Margin = new Thickness(0, 2, 0, 2) };
            b.SetResourceReference(StyleProperty, "NavButton");
            var k = key;
            b.Click += (_, _) => Navigate(k);
            navButtons[key] = b;
            NavPanel.Children.Add(b);
        }

        // Bottom: admin status + version
        if (!Admin.IsAdmin)
        {
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text("Standard mode", 13, true));
            var d = Ui.Text("Repair tools need administrator access.", 12, false, "MutedBrush");
            d.Margin = new Thickness(0, 3, 0, 8);
            sp.Children.Add(d);
            sp.Children.Add(Ui.Btn("Restart as admin", () => _ = EnsureAdminAsync(
                "Some tools, like System Repair, can only run with administrator rights."), "SecondaryButton"));
            BottomPanel.Children.Add(Ui.Card(sp, 14, 12));
        }
        else
        {
            BottomPanel.Children.Add(Ui.Text("✓ Administrator mode", 12.5, true, "GoodBrush"));
        }
        var ver = Ui.Text($"Version {Branding.VersionText}", 11.5, false, "MutedBrush");
        ver.Margin = new Thickness(4, 10, 0, 0);
        BottomPanel.Children.Add(ver);
    }

    void HighlightNav(string key)
    {
        var active = key == "boost" ? "dashboard" : key;
        foreach (var (k, b) in navButtons)
        {
            bool sel = k == active;
            b.SetResourceReference(Control.BackgroundProperty, sel ? "NavSelectedBrush" : "SidebarBrush");
            b.SetResourceReference(Control.ForegroundProperty, sel ? "AccentBrush" : "TextBrush");
        }
    }

    // ---------- Navigation ----------

    PageView Create(string key) => key switch
    {
        "ram" => new RamView(),
        "clean" => new CleanView(),
        "game" => new GameView(),
        "health" => new HealthView(),
        "repair" => new RepairView(),
        "history" => new HistoryView(),
        "settings" => new SettingsView(),
        "boost" => new BoostView(),
        _ => new DashboardView()
    };

    public void Navigate(string key)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => Navigate(key)); return; }
        if (key != "boost" && !NavItems.Any(n => n.Key == key)) key = "dashboard";

        current?.OnHidden();
        currentKey = key;
        HighlightNav(key);

        var view = Create(key);
        current = view;
        PageHost.Content = view;

        view.Opacity = 0;
        var tt = new TranslateTransform(0, 14);
        view.RenderTransform = tt;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        view.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(280)) { EasingFunction = ease });

        view.OnShown();
    }

    public void StartBoost() => Navigate("boost");

    // ---------- Modal dialogs ----------

    public Task<bool> Confirm(string title, string message, string yes = "Continue", string no = "Cancel")
        => ShowModal(title, message, yes, no);

    public Task Info(string title, string message, string ok = "OK") => ShowModal(title, message, ok, null);

    Task<bool> ShowModal(string title, string message, string yes, string? no)
    {
        var tcs = new TaskCompletionSource<bool>();
        var panel = new StackPanel();
        panel.Children.Add(Ui.Text(title, 20, true, "TextBrush", Ui.Display));
        var msg = Ui.Text(message, 14, false, "MutedBrush");
        msg.Margin = new Thickness(0, 10, 0, 24);
        panel.Children.Add(msg);

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        if (no != null)
        {
            var nb = Ui.Btn(no, () => Close(false), "SecondaryButton");
            nb.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(nb);
        }
        row.Children.Add(Ui.Btn(yes, () => Close(true)));
        panel.Children.Add(row);

        var card = Ui.Card(panel, 28, 18);
        card.Width = 460;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        card.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(0.94, 0.94);
        card.RenderTransform = scale;

        Overlay.Children.Clear();
        Overlay.Children.Add(card);
        Overlay.Visibility = Visibility.Visible;
        Overlay.Opacity = 0;
        Overlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });

        void Close(bool result)
        {
            Overlay.Visibility = Visibility.Collapsed;
            Overlay.Children.Clear();
            tcs.TrySetResult(result);
        }
        return tcs.Task;
    }

    public async Task<bool> EnsureAdminAsync(string reason)
    {
        if (Admin.IsAdmin) return true;
        bool ok = await Confirm("Administrator access needed",
            reason + "\n\nVelora will restart and Windows will ask for your permission once.",
            "Restart as administrator", "Not now");
        if (!ok) return false;

        if (Admin.Relaunch($"--page={currentKey}")) Application.Current.Shutdown();
        else Toast("Administrator permission wasn't granted.", ToastKind.Warning);
        return false;
    }

    // ---------- Toasts ----------

    public void Toast(string message, ToastKind kind = ToastKind.Info)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => Toast(message, kind)); return; }
        if (kind != ToastKind.Error && !Config.Current.Notifications) return;

        string brush = kind switch
        {
            ToastKind.Success => "GoodBrush",
            ToastKind.Error => "BadBrush",
            ToastKind.Warning => "WarnBrush",
            _ => "AccentBrush"
        };

        var accent = new Border { Width = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 12, 0) };
        accent.SetResourceReference(Border.BackgroundProperty, brush);
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(accent);
        var text = Ui.Text(message, 13.5);
        text.MaxWidth = 340;
        content.Children.Add(text);

        var card = Ui.Card(content, 14, 12);
        card.Margin = new Thickness(0, 8, 0, 0);
        card.Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 2, Opacity = 0.35, Color = Colors.Black };
        ToastHost.Children.Add(card);
        Ui.FadeIn(card, 220, 12);

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
            fade.Completed += (_, _) => ToastHost.Children.Remove(card);
            card.BeginAnimation(OpacityProperty, fade);
        };
        timer.Start();
    }

    // ---------- Theme ----------

    public void RefreshTheme()
    {
        ThemeService.Apply(Config.Current.Theme);
        UpdateTitleBar();
        HighlightNav(currentKey);
        Navigate(currentKey == "boost" ? "dashboard" : currentKey);
    }

    void UpdateTitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            int dark = ThemeService.IsDark ? 1 : 0;
            Native.DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
        }
        catch { }
    }

    // ---------- Onboarding & updates ----------

    void ShowOnboarding()
    {
        OnboardHost.Visibility = Visibility.Visible;
        OnboardHost.Children.Clear();
        OnboardHost.Children.Add(new OnboardingView(() =>
        {
            Config.Current.OnboardingDone = true;
            Config.Save();
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(220));
            fade.Completed += (_, _) => { OnboardHost.Visibility = Visibility.Collapsed; OnboardHost.Children.Clear(); OnboardHost.Opacity = 1; };
            OnboardHost.BeginAnimation(OpacityProperty, fade);
        }));
    }

    async Task CheckUpdatesQuietlyAsync()
    {
        var info = await UpdateService.CheckAsync();
        if (!info.Available) return;
        Toast($"{info.Message} Open Settings > Updates to install it.", ToastKind.Info);
        if (currentKey == "dashboard") Navigate("dashboard");   // show the update banner
    }
}
