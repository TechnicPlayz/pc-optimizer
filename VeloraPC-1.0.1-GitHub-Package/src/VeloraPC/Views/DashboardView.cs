using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using VeloraPC.Core;
using VeloraPC.Services;
using VeloraPC.UI;

namespace VeloraPC.Views;

public sealed class DashboardView : PageView
{
    readonly TextBlock headline = Ui.Text("Your PC is ready.", 36, true, "TextBrush", Ui.Display);
    readonly TextBlock ramVal = Ui.Text("—", 26, true, "TextBrush", Ui.Display);
    readonly TextBlock ramSub = Ui.Text("", 12.5, false, "MutedBrush");
    readonly ProgressBar ramBar = Ui.Bar();
    readonly TextBlock cpuVal = Ui.Text("—", 26, true, "TextBrush", Ui.Display);
    readonly TextBlock cpuSub = Ui.Text("Processor load", 12.5, false, "MutedBrush");
    readonly ProgressBar cpuBar = Ui.Bar();
    readonly TextBlock diskVal = Ui.Text("—", 26, true, "TextBrush", Ui.Display);
    readonly TextBlock diskSub = Ui.Text("", 12.5, false, "MutedBrush");
    readonly ProgressBar diskBar = Ui.Bar();
    readonly TextBlock healthVal = Ui.Text("Checking…", 26, true, "MutedBrush", Ui.Display);
    readonly TextBlock healthSub = Ui.Text("", 12.5, false, "MutedBrush");
    readonly UniformGrid grid = new() { Columns = 4, Margin = new Thickness(-6, 0, -6, 0) };
    DispatcherTimer? timer;

    public DashboardView() : base(null, null)
    {
        Body.Children.Add(Ui.Text(Fmt.Greeting(), 15, false, "MutedBrush"));
        headline.Margin = new Thickness(0, 2, 0, 22);
        Body.Children.Add(headline);

        if (UpdateService.Pending is { } upd)
        {
            var msg = new StackPanel();
            msg.Children.Add(Ui.Text($"⬆ Update available — Velora {upd.Latest}", 14.5, true, "AccentBrush"));
            msg.Children.Add(Ui.Text("A new version is ready to install.", 12.5, false, "MutedBrush"));
            var banner = Ui.Card(Ui.Row(msg, Ui.Btn("UPDATE", () => App.Shell.Navigate("settings"), "PrimaryButton", 110)),
                16, 14, "HeroBrush", "AccentBorderBrush");
            banner.Margin = new Thickness(0, 0, 0, 18);
            Body.Children.Add(banner);
            Ui.FadeIn(banner, 340, 12);
        }

        var hero = BuildHero();
        Body.Children.Add(hero);
        Ui.FadeIn(hero, 420, 20);

        var gridHost = new StackPanel { Margin = new Thickness(0, 22, 0, 0) };
        var cards = new[]
        {
            Stat("🧠", "RAM", ramVal, ramSub, ramBar),
            Stat("💾", "STORAGE", diskVal, diskSub, diskBar),
            Stat("⚙️", "CPU", cpuVal, cpuSub, cpuBar),
            Stat("🩺", "PC HEALTH", healthVal, healthSub, null)
        };
        int delay = 140;
        foreach (var card in cards) { grid.Children.Add(card); Ui.FadeIn(card, 380, 18, delay); delay += 80; }
        gridHost.Children.Add(grid);
        Body.Children.Add(gridHost);
        SizeChanged += (_, e) => grid.Columns = e.NewSize.Width < 880 ? 2 : 4;

        var left = new StackPanel();
        left.Children.Add(Ui.Text("LAST OPTIMIZATION", 11.5, true, "MutedBrush"));
        var when = Ui.Text(Fmt.When(Config.Current.LastBoostUtc), 18, true);
        when.Margin = new Thickness(0, 4, 0, 0);
        left.Children.Add(when);
        var last = Ui.Card(Ui.Row(left, Ui.Btn("View history", () => App.Shell.Navigate("history"), "GhostButton")), 20, 16, hover: true);
        last.Margin = new Thickness(0, 16, 0, 0);
        Body.Children.Add(last);
        Ui.FadeIn(last, 380, 16, 480);

        var (total, free) = SystemInfo.SystemDrive();
        if (total > 0)
        {
            diskVal.Text = $"{Fmt.Bytes(free)} free";
            diskSub.Text = $"of {Fmt.Bytes(total)}";
            diskBar.Value = (total - free) * 100.0 / total;
        }

        Tick();
        _ = LoadHealthAsync();
        Unloaded += (_, _) => timer?.Stop();
    }

    async Task LoadHealthAsync()
    {
        try
        {
            var checks = await Task.Run(HealthService.Evaluate);
            var overall = HealthService.Overall(checks);
            headline.Text = overall == HealthStatus.Good ? "Your PC is ready." : "Your PC could use some care.";
            healthVal.Text = overall == HealthStatus.Good ? "Good" : overall == HealthStatus.Attention ? "Attention" : "Warning";
            Ui.Fg(healthVal, HealthService.BrushKey(overall));
            int n = checks.Count(c => c.Status is HealthStatus.Attention or HealthStatus.Warning);
            healthSub.Text = n == 0 ? "All checks look fine" : $"{n} item(s) need a look";
        }
        catch (Exception ex)
        {
            Logger.Error("Dashboard health check failed", ex);
            healthVal.Text = "Unknown";
            healthSub.Text = "Couldn't read status";
        }
    }

    static Border BuildHero()
    {
        var sp = new StackPanel();
        var title = new StackPanel { Orientation = Orientation.Horizontal };
        title.Children.Add(Ui.Icon("🚀", 34));
        var t = Ui.Text("ULTRA ULTRA FAST PC", 28, true, "TextBrush", Ui.Display);
        t.Margin = new Thickness(14, 0, 0, 0);
        t.VerticalAlignment = VerticalAlignment.Center;
        title.Children.Add(t);
        sp.Children.Add(title);

        var sub = Ui.Text("One click. Full PC optimization.", 17, false, "MutedBrush");
        sub.Margin = new Thickness(0, 8, 0, 22);
        sp.Children.Add(sub);

        var btn = Ui.Btn("BOOST MY PC", () => App.Shell.StartBoost(), "HeroButton", 300);
        btn.HorizontalAlignment = HorizontalAlignment.Left;
        var glow = new DropShadowEffect
        {
            Color = ((SolidColorBrush)Ui.Res("AccentBrush")).Color,
            BlurRadius = 14, ShadowDepth = 0, Opacity = 0.55
        };
        btn.Effect = glow;
        glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty,
            new DoubleAnimation(14, 32, TimeSpan.FromSeconds(1.9))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() });
        sp.Children.Add(btn);

        var cap = Ui.Text("Cleans temporary files and the Recycle Bin, frees memory, checks Windows health and applies " +
                          "safe settings — only what your PC needs. Your personal files are never touched.",
                          12.5, false, "MutedBrush");
        cap.Margin = new Thickness(0, 16, 0, 0);
        cap.MaxWidth = 560;
        cap.HorizontalAlignment = HorizontalAlignment.Left;
        sp.Children.Add(cap);

        return Ui.Card(sp, 32, 22, "HeroBrush", "AccentBorderBrush");
    }

    static Border Stat(string emoji, string label, TextBlock value, TextBlock sub, ProgressBar? bar)
    {
        var sp = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal };
        head.Children.Add(Ui.Icon(emoji, 15));
        var l = Ui.Text(label, 11.5, true, "MutedBrush");
        l.Margin = new Thickness(8, 0, 0, 0);
        l.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(l);
        sp.Children.Add(head);
        value.Margin = new Thickness(0, 14, 0, 2);
        sp.Children.Add(value);
        sp.Children.Add(sub);
        if (bar != null) { bar.Margin = new Thickness(0, 14, 0, 0); sp.Children.Add(bar); }
        var card = Ui.Card(sp, 18, 16, hover: true);
        card.Margin = new Thickness(6);
        return card;
    }

    void Tick()
    {
        var m = SystemInfo.Memory();
        ramVal.Text = $"{Fmt.Gb(m.Used)} / {m.Total / 1073741824.0:0} GB";
        ramSub.Text = $"{m.Percent:0}% in use";
        Ui.SmoothTo(ramBar, m.Percent, 400);

        double cpu = SystemInfo.CpuPercent();
        cpuVal.Text = $"{cpu:0}%";
        Ui.SmoothTo(cpuBar, cpu, 400);
    }

    public override void OnShown()
    {
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    public override void OnHidden() => timer?.Stop();
}
