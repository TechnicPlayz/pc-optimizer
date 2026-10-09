using System.Windows;
using System.Windows.Controls;
using VeloraPC.Core;
using VeloraPC.Services;
using VeloraPC.UI;

namespace VeloraPC.Views;

public sealed class CleanView : PageView
{
    readonly StackPanel host = new();
    List<CleanCategory> cats = new();
    CancellationTokenSource? cts;
    TextBlock? totalText;
    Button? cleanBtn;

    public CleanView() : base("🧹 Deep Clean", "Scan first, review what's found, then clean. Personal files are never touched.")
    {
        Body.Children.Add(host);
    }

    public override void OnShown() => _ = ScanAsync();
    public override void OnHidden() => cts?.Cancel();

    void Show(UIElement e)
    {
        host.Children.Clear();
        host.Children.Add(e);
        Ui.FadeIn(e, 220, 8);
    }

    static StackPanel Busy(string message)
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 50, 0, 0) };
        var s = Ui.Spinner(44); s.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(s);
        var t = Ui.Text(message, 15, false, "MutedBrush");
        t.Margin = new Thickness(0, 16, 0, 0); t.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(t);
        return sp;
    }

    async Task ScanAsync()
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        Show(Busy("Scanning your PC…"));
        try
        {
            cats = await Cleanup.ScanAsync(cts.Token);
            RenderReady();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Logger.Error("Scan failed", ex);
            Show(Ui.ErrorPanel(Friendly.Message(ex), () => _ = ScanAsync()));
        }
    }

    void UpdateTotal()
    {
        long total = cats.Where(c => c.Selected).Sum(c => c.Bytes);
        if (totalText != null) totalText.Text = Fmt.Bytes(total);
        if (cleanBtn != null) cleanBtn.IsEnabled = cats.Any(c => c.Selected && c.Items > 0);
    }

    void RenderReady()
    {
        var root = new StackPanel();

        var head = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
        totalText = Ui.Text("", 46, true, "TextBrush", Ui.Display);
        head.Children.Add(totalText);
        head.Children.Add(Ui.Text("available to clean", 15, false, "MutedBrush"));
        root.Children.Add(head);

        foreach (var cat in cats)
        {
            var c = cat;
            var sw = new CheckBox { IsChecked = c.Selected, IsEnabled = c.Items > 0, Margin = new Thickness(0, 0, 16, 0) };
            sw.SetResourceReference(StyleProperty, "SwitchStyle");
            sw.Click += (_, _) => { c.Selected = sw.IsChecked == true; UpdateTotal(); };

            var info = new StackPanel();
            info.Children.Add(Ui.Text(c.Name, 16, true));
            info.Children.Add(Ui.Text(c.Description, 13, false, "MutedBrush"));

            var locations = new StackPanel { Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
            foreach (var loc in c.Locations)
            {
                var line = Ui.Text($"{loc.FullPath}  —  {Fmt.Bytes(loc.Bytes)}", 12, false, "MutedBrush");
                line.Margin = new Thickness(0, 2, 0, 2);
                locations.Children.Add(line);
            }
            if (c.Locations.Count == 0) locations.Children.Add(Ui.Text("Nothing to clean here.", 12, false, "MutedBrush"));
            info.Children.Add(locations);

            var review = Ui.Btn("Review", () =>
                locations.Visibility = locations.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible,
                "GhostButton");
            review.Padding = new Thickness(10, 4, 10, 4);
            review.HorizontalAlignment = HorizontalAlignment.Left;
            review.Margin = new Thickness(-10, 4, 0, 0);
            info.Children.Add(review);

            var size = Ui.Text(Fmt.Bytes(c.Bytes), 20, true, "TextBrush", Ui.Display);
            size.VerticalAlignment = VerticalAlignment.Top;

            var left = new StackPanel { Orientation = Orientation.Horizontal };
            sw.VerticalAlignment = VerticalAlignment.Top;
            left.Children.Add(sw);
            left.Children.Add(info);

            var card = Ui.Card(Ui.Row(left, size), 20, 16);
            card.Margin = new Thickness(0, 0, 0, 12);
            root.Children.Add(card);
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        cleanBtn = Ui.Btn("CLEAN NOW", () => _ = CleanAsync(), "PrimaryButton", 180);
        cleanBtn.Margin = new Thickness(0, 0, 12, 0);
        actions.Children.Add(cleanBtn);
        actions.Children.Add(Ui.Btn("RESCAN", () => _ = ScanAsync(), "SecondaryButton", 140));
        root.Children.Add(actions);

        if (!Admin.IsAdmin)
        {
            var hint = Ui.Text("Some protected system locations are only included when Velora runs as administrator.", 12.5, false, "MutedBrush");
            hint.Margin = new Thickness(0, 16, 0, 0);
            root.Children.Add(hint);
            var more = Ui.Btn("Include protected locations", () => _ = App.Shell.EnsureAdminAsync(
                "Cleaning Windows' own temp and update folders needs administrator rights."), "GhostButton");
            more.HorizontalAlignment = HorizontalAlignment.Left;
            more.Margin = new Thickness(-18, 0, 0, 0);
            root.Children.Add(more);
        }

        Show(root);
        UpdateTotal();
    }

    async Task CleanAsync()
    {
        var chosen = cats.Where(c => c.Selected && c.Items > 0).ToList();
        if (chosen.Count == 0) return;

        if (Config.Current.ConfirmCleanup)
        {
            bool ok = await App.Shell.Confirm("Clean selected items?",
                $"This will remove about {Fmt.Bytes(chosen.Sum(c => c.Bytes))} from: {string.Join(", ", chosen.Select(c => c.Name))}.",
                "Clean now", "Cancel");
            if (!ok) return;
        }

        cts?.Cancel();
        cts = new CancellationTokenSource();

        var sp = new StackPanel { Margin = new Thickness(0, 40, 0, 0), MaxWidth = 520, HorizontalAlignment = HorizontalAlignment.Center };
        var t = Ui.Text("Cleaning…", 20, true, "TextBrush", Ui.Display);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(t);
        var bar = Ui.Bar(); bar.Margin = new Thickness(0, 18, 0, 18); bar.Height = 8;
        sp.Children.Add(bar);
        var cancel = Ui.Btn("CANCEL", () => cts?.Cancel(), "SecondaryButton", 130);
        cancel.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(cancel);
        Show(sp);

        try
        {
            var progress = new Progress<double>(v => Ui.SmoothTo(bar, v * 100, 250));
            var res = await Cleanup.CleanAsync(chosen, progress, cts.Token);

            HistoryStore.Add("clean", "Deep Clean", $"{Fmt.Bytes(res.Bytes)} removed",
                $"Categories: {string.Join(", ", chosen.Select(c => c.Name))}\nFiles removed: {res.Files}\nSkipped (in use): {res.Skipped}");
            ShowDone(res);
        }
        catch (OperationCanceledException)
        {
            App.Shell.Toast("Cleaning cancelled. Anything already removed stays removed.", ToastKind.Warning);
            _ = ScanAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("Clean failed", ex);
            Show(Ui.ErrorPanel(Friendly.Message(ex), () => _ = ScanAsync()));
        }
    }

    void ShowDone(CleanResult res)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 30, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        var ok = Ui.Text("✓", 54, true, "GoodBrush"); ok.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(ok);
        var big = Ui.Text($"{Fmt.Bytes(res.Bytes)} cleaned", 34, true, "TextBrush", Ui.Display);
        big.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(big);
        var sub = Ui.Text(res.Skipped > 0
            ? $"{res.Skipped} file(s) were in use and left alone."
            : "Nothing was in use, so everything selected was removed.", 14, false, "MutedBrush");
        sub.HorizontalAlignment = HorizontalAlignment.Center; sub.Margin = new Thickness(0, 6, 0, 22);
        sp.Children.Add(sub);
        var b = Ui.Btn("SCAN AGAIN", () => _ = ScanAsync(), "PrimaryButton", 170);
        b.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(b);
        Show(sp);
        App.Shell.Toast($"{Fmt.Bytes(res.Bytes)} cleaned", ToastKind.Success);
    }
}
