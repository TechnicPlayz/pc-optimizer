using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VeloraPC.Core;
using VeloraPC.Services;
using VeloraPC.UI;

namespace VeloraPC.Views;

/// <summary>Live optimization screen + results. Progress comes only from real completed operations.</summary>
public sealed class BoostView : PageView
{
    readonly CircularProgress ring = new() { Width = 200, Height = 200, Thickness = 12 };
    readonly TextBlock pct = Ui.Text("0%", 40, true, "TextBrush", Ui.Display);
    readonly TextBlock heading = Ui.Text("Optimizing your PC...", 24, true, "TextBrush", Ui.Display);
    readonly List<(TextBlock Icon, TextBlock Detail)> rows = new();
    readonly Button cancel;
    CancellationTokenSource? cts;
    bool started;

    public BoostView() : base(null, null) { cancel = Ui.Btn("CANCEL", () => cts?.Cancel(), "SecondaryButton", 140); BuildRunning(); }

    void BuildRunning()
    {
        Body.Children.Clear();
        var center = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };

        var tag = Ui.Text("🚀 ULTRA ULTRA FAST PC", 14, true, "AccentBrush");
        tag.HorizontalAlignment = HorizontalAlignment.Center;
        center.Children.Add(tag);

        heading.HorizontalAlignment = HorizontalAlignment.Center;
        heading.Margin = new Thickness(0, 8, 0, 26);
        center.Children.Add(heading);

        var ringHost = new Grid { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Center };
        ringHost.Children.Add(ring);
        pct.HorizontalAlignment = HorizontalAlignment.Center;
        pct.VerticalAlignment = VerticalAlignment.Center;
        ringHost.Children.Add(pct);
        center.Children.Add(ringHost);

        var list = new StackPanel { Width = 460 };
        rows.Clear();
        foreach (var title in BoostService.StepTitles)
        {
            var g = new Grid { Margin = new Thickness(0, 7, 0, 7) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = Ui.Text("○", 16, true, "MutedBrush");
            icon.TextWrapping = TextWrapping.NoWrap;
            var name = Ui.Text(title, 14.5);
            Grid.SetColumn(name, 1);
            var detail = Ui.Text("", 12.5, false, "MutedBrush");
            detail.TextAlignment = TextAlignment.Right;
            detail.MaxWidth = 190;
            Grid.SetColumn(detail, 2);

            g.Children.Add(icon); g.Children.Add(name); g.Children.Add(detail);
            list.Children.Add(g);
            rows.Add((icon, detail));
        }
        var card = Ui.Card(list, 18, 16);
        card.Margin = new Thickness(0, 30, 0, 0);
        center.Children.Add(card);

        cancel.HorizontalAlignment = HorizontalAlignment.Center;
        cancel.Margin = new Thickness(0, 22, 0, 0);
        center.Children.Add(cancel);
        Body.Children.Add(center);
    }

    public override void OnShown()
    {
        if (started) return;
        started = true;
        _ = RunAsync();
    }

    public override void OnHidden() => cts?.Cancel();

    async Task RunAsync()
    {
        if (Config.Current.ConfirmCleanup)
        {
            bool ok = await App.Shell.Confirm("Start ULTRA ULTRA FAST PC?",
                "Velora will clean temporary files and empty the Recycle Bin, free memory, check Windows health and apply " +
                "safe settings. Your personal files are never touched.", "Start boost", "Cancel");
            if (!ok) { App.Shell.Navigate("dashboard"); return; }
        }

        cts = new CancellationTokenSource();
        var progress = new Progress<BoostEvent>(OnEvent);
        try
        {
            var result = await BoostService.RunAsync(progress, cts.Token);
            ShowResults(result);
        }
        catch (Exception ex)
        {
            Logger.Error("Boost failed", ex);
            Body.Children.Clear();
            Body.Children.Add(Ui.ErrorPanel(Friendly.Message(ex), () => { started = false; BuildRunning(); OnShown(); }));
        }
    }

    void OnEvent(BoostEvent e)
    {
        if (e.Step < 0 || e.Step >= rows.Count) return;
        var (icon, detail) = rows[e.Step];

        switch (e.State)
        {
            case StepState.Running:
                icon.Text = "●"; Ui.Fg(icon, "AccentBrush");
                icon.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.3, TimeSpan.FromMilliseconds(600))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
                break;
            case StepState.Done:
                Settle(icon, "✓", "GoodBrush"); break;
            case StepState.Skipped:
                Settle(icon, "–", "MutedBrush"); break;
            case StepState.Failed:
                Settle(icon, "!", "BadBrush"); break;
        }
        if (e.Detail.Length > 0) detail.Text = e.Detail;

        ring.BeginAnimation(CircularProgress.ValueProperty,
            new DoubleAnimation(e.Overall, TimeSpan.FromMilliseconds(300)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        pct.Text = $"{e.Overall * 100:0}%";
    }

    static void Settle(TextBlock icon, string glyph, string brush)
    {
        icon.BeginAnimation(OpacityProperty, null);
        icon.Opacity = 1;
        icon.Text = glyph;
        Ui.Fg(icon, brush);
    }

    // ---------------- Results ----------------

    void ShowResults(BoostResult r)
    {
        Body.Children.Clear();
        var c = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 720, Margin = new Thickness(0, 10, 0, 0) };

        // animated badge
        var badge = new Border { Width = 84, Height = 84, CornerRadius = new CornerRadius(42), HorizontalAlignment = HorizontalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5) };
        badge.SetResourceReference(Border.BackgroundProperty, r.Cancelled ? "WarnBrush" : "GoodBrush");
        var mark = new TextBlock { Text = r.Cancelled ? "■" : "✓", FontSize = 40, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        badge.Child = mark;
        var scale = new ScaleTransform(0.3, 0.3);
        badge.RenderTransform = scale;
        var pop = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.3, 1, TimeSpan.FromMilliseconds(450)) { EasingFunction = pop });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.3, 1, TimeSpan.FromMilliseconds(450)) { EasingFunction = pop });
        c.Children.Add(badge);

        var title = Ui.Text(r.Cancelled ? "BOOST CANCELLED" : "⚡ BOOST COMPLETE", 30, true, "TextBrush", Ui.Display);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.Margin = new Thickness(0, 18, 0, 4);
        c.Children.Add(title);
        var sub = Ui.Text(r.Cancelled ? "Stopped safely. Anything already finished was kept." : "Here's what changed on your PC.", 14.5, false, "MutedBrush");
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        sub.Margin = new Thickness(0, 0, 0, 24);
        c.Children.Add(sub);

        var tiles = new UniformGrid { Columns = 4, Margin = new Thickness(-6, 0, -6, 0) };
        tiles.Children.Add(Tile(Fmt.Bytes(r.BytesCleaned), "cleaned", null));
        tiles.Children.Add(Tile(Fmt.Bytes(r.MemoryFreed), "memory freed", null));
        tiles.Children.Add(Tile(r.TasksCompleted.ToString(), "maintenance tasks completed", null));
        tiles.Children.Add(Tile(r.Problems.ToString(), r.Problems == 1 ? "problem found" : "problems found", r.Problems == 0 ? "GoodBrush" : "WarnBrush"));
        c.Children.Add(tiles);
        Ui.FadeIn(tiles, 300, 12, 150);

        // What we did
        var did = new StackPanel();
        did.Children.Add(Ui.Text("What we did", 16, true, "TextBrush", Ui.Display));
        foreach (var (state, text) in r.Lines)
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var (glyph, brush) = state switch
            {
                StepState.Done => ("✓", "GoodBrush"),
                StepState.Skipped => ("–", "MutedBrush"),
                _ => ("!", "BadBrush")
            };
            var g = Ui.Text(glyph, 14, true, brush); g.Width = 22; g.TextWrapping = TextWrapping.NoWrap;
            line.Children.Add(g);
            var t = Ui.Text(text, 13.5, false, state == StepState.Skipped ? "MutedBrush" : "TextBrush");
            t.MaxWidth = 600;
            line.Children.Add(t);
            did.Children.Add(line);
        }
        foreach (var note in r.Notes)
        {
            var n = Ui.Text("ⓘ " + note, 13, false, "WarnBrush");
            n.Margin = new Thickness(0, 12, 0, 0);
            did.Children.Add(n);
        }
        var didCard = Ui.Card(did, 22, 16);
        didCard.Margin = new Thickness(0, 22, 0, 0);
        c.Children.Add(didCard);

        // Details (hidden until requested)
        var details = Ui.Card(Ui.LogBox(r.Details.ToString()), 14, 14);
        details.Margin = new Thickness(0, 14, 0, 0);
        details.Visibility = Visibility.Collapsed;
        c.Children.Add(details);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) };
        var vd = Ui.Btn("VIEW DETAILS", () => details.Visibility = details.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible, "SecondaryButton", 160);
        vd.Margin = new Thickness(0, 0, 12, 0);
        buttons.Children.Add(vd);
        buttons.Children.Add(Ui.Btn("DONE", () => App.Shell.Navigate("dashboard"), "PrimaryButton", 160));
        c.Children.Add(buttons);

        Body.Children.Add(c);
        if (!r.Cancelled) App.Shell.Toast("Boost complete", ToastKind.Success);
    }

    static Border Tile(string value, string label, string? brush)
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var v = Ui.Text(value, 26, true, brush ?? "TextBrush", Ui.Display);
        v.TextAlignment = TextAlignment.Center;
        var l = Ui.Text(label, 12.5, false, "MutedBrush");
        l.TextAlignment = TextAlignment.Center;
        sp.Children.Add(v); sp.Children.Add(l);
        var card = Ui.Card(sp, 16, 16);
        card.Margin = new Thickness(6);
        return card;
    }
}
