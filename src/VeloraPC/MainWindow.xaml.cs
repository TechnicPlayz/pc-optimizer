using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        ("clean",     "🧹", "Storage Cleaner"),
        ("game",      "🎮", "Game Boost"),
        ("health",    "🩺", "PC Health"),
        ("repair",    "🔧", "System Repair"),
        ("history",   "🕘", "History"),
        ("settings",  "⚙️", "Settings"),
    };

    readonly Dictionary<string, Button> navButtons = new();
    readonly Dictionary<string, Border> navIndicators = new();
    PageView? current;
    string currentKey = "dashboard";
    Action<int>? closeModal;

    public MainWindow()
    {
        InitializeComponent();
        RestoreWindowSize();
        BuildSidebar();

        // gentle fade-in when the window opens
        if (Content is UIElement root)
        {
            root.Opacity = 0;
            Loaded += (_, _) => root.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(380)) { EasingFunction = Ui.Ease });
        }

        SourceInitialized += (_, _) => UpdateTitleBar();
        PreviewKeyDown += OnKey;
        Closing += OnClosing;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void Start()
    {
        var page = App.StartupArgs.FirstOrDefault(a => a.StartsWith("--page="))?.Substring(7) ?? "dashboard";
        Navigate(page);

        if (!Config.Current.OnboardingDone) ShowOnboarding();
        else if (Config.Current.AutoCheckUpdates) _ = CheckUpdatesQuietlyAsync();
    }

    // ---------- Window size / keyboard / system theme ----------

    void RestoreWindowSize()
    {
        var s = Config.Current;
        if (s.WindowWidth is double w && s.WindowHeight is double h)
        {
            var area = SystemParameters.WorkArea;
            Width = Math.Clamp(w, MinWidth, Math.Max(MinWidth, area.Width));
            Height = Math.Clamp(h, MinHeight, Math.Max(MinHeight, area.Height));
        }
        if (s.WindowMaximized) WindowState = WindowState.Maximized;
    }

    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            var s = Config.Current;
            s.WindowMaximized = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Normal) { s.WindowWidth = Width; s.WindowHeight = Height; }
            Config.Save();
        }
        catch { }
    }

    void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && Overlay.Visibility == Visibility.Visible)
        {
            closeModal?.Invoke(0);
            e.Handled = true;
            return;
        }
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && Overlay.Visibility != Visibility.Visible &&
            e.Key >= Key.D1 && e.Key <= Key.D8)
        {
            int idx = e.Key - Key.D1;
            if (idx < NavItems.Length) { Navigate(NavItems[idx].Key); e.Handled = true; }
        }
    }

    void OnUserPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category != Microsoft.Win32.UserPreferenceCategory.General || Config.Current.Theme != "System") return;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (current is BoostView { IsRunning: true }) return;   // never interrupt a boost
            RefreshTheme();
        }));
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

        int delay = 60;
        foreach (var (key, emoji, label) in NavItems)
        {
            var indicator = new Border
            {
                Width = 3, Height = 18, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 9, 0),
                Opacity = 0, VerticalAlignment = VerticalAlignment.Center
            };
            indicator.SetResourceReference(Border.BackgroundProperty, "AccentBrush");

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(indicator);
            var ic = Ui.Icon(emoji, 16); ic.Width = 26;
            row.Children.Add(ic);
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });

            var b = new Button { Content = row, Margin = new Thickness(0, 2, 0, 2) };
            b.SetResourceReference(StyleProperty, "NavButton");
            var k = key;
            b.Click += (_, _) => Navigate(k);
            navButtons[key] = b;
            navIndicators[key] = indicator;
            NavPanel.Children.Add(b);
            Ui.FadeIn(b, 360, 10, delay);
            delay += 45;
        }

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
            navIndicators[k].BeginAnimation(OpacityProperty,
                new DoubleAnimation(sel ? 1 : 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = Ui.Ease });
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

        if (current is BoostView { IsRunning: true } && key != "boost")
        {
            Toast("A boost is running. Press Cancel on that screen to stop it first.", ToastKind.Warning);
            return;
        }

        current?.OnHidden();
        currentKey = key;
        HighlightNav(key);

        var view = Create(key);
        current = view;
        PageHost.Content = view;

        // macOS-style entrance: fade + gentle rise + tiny scale, with a smooth deceleration
        var scale = new ScaleTransform(0.985, 0.985);
        var move = new TranslateTransform(0, 18);
        var group = new TransformGroup();
        group.Children.Add(scale);
        group.Children.Add(move);
        view.RenderTransformOrigin = new Point(0.5, 0.2);
        view.RenderTransform = group;
        view.Opacity = 0;
        view.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)) { EasingFunction = Ui.Ease });
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(560)) { EasingFunction = Ui.Ease });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.985, 1, TimeSpan.FromMilliseconds(560)) { EasingFunction = Ui.Ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.985, 1, TimeSpan.FromMilliseconds(560)) { EasingFunction = Ui.Ease });

        view.OnShown();
    }

    public void StartBoost() => Navigate("boost");

    // ---------- Modal dialogs ----------

    public async Task<bool> Confirm(string title, string message, string yes = "Continue", string no = "Cancel")
    {
        int clicked = await ShowChoice(title, message, new[] { no, yes });
        return clicked == 1;
    }

    public async Task Info(string title, string message, string ok = "OK")
    {
        await ShowChoice(title, message, new[] { ok });
    }

    public Task<int> Choose(string title, string message, string[] options) => ShowChoice(title, message, options);

    Task<int> ShowChoice(string title, string message, string[] options)
    {
        closeModal?.Invoke(0);   // resolve any dialog that is still open

        var tcs = new TaskCompletionSource<int>();
        var panel = new StackPanel();
        panel.Children.Add(Ui.Text(title, 20, true, "TextBrush", Ui.Display));
        var msg = Ui.Text(message, 14, false, "MutedBrush");
        msg.Margin = new Thickness(0, 10, 0, 24);
        panel.Children.Add(msg);

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        for (int i = 0; i < options.Length; i++)
        {
            int idx = i;
            bool primary = i == options.Length - 1;
            var b = Ui.Btn(options[i], () => Close(idx), primary ? "PrimaryButton" : "SecondaryButton");
            if (!primary) b.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(b);
        }
        panel.Children.Add(row);

        var card = Ui.Card(panel, 28, 18);
        card.Width = options.Length > 2 ? 640 : 460;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        card.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(0.92, 0.92);
        card.RenderTransform = scale;

        Overlay.Children.Clear();
        Overlay.Children.Add(card);
        Overlay.Visibility = Visibility.Visible;
        Overlay.Opacity = 0;
        Overlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        var spring = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(340)) { EasingFunction = spring });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.92, 1, TimeSpan.FromMilliseconds(340)) { EasingFunction = spring });

        closeModal = Close;
        void Close(int result)
        {
            Overlay.Visibility = Visibility.Collapsed;
            Overlay.Children.Clear();
            closeModal = null;
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
        card.Effect = new DropShadowEffect { BlurRadius = 20, ShadowDepth = 3, Opacity = 0.4, Color = Colors.Black };
        ToastHost.Children.Add(card);
        Ui.FadeIn(card, 300, 16);

        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(280));
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
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260));
            fade.Completed += (_, _) =>
            {
                OnboardHost.BeginAnimation(OpacityProperty, null);
                OnboardHost.Visibility = Visibility.Collapsed;
                OnboardHost.Children.Clear();
                OnboardHost.Opacity = 1;
            };
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
