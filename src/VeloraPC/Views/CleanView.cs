using System.Windows;
using System.Windows.Controls;
using VeloraPC.Core;
using VeloraPC.Services;
using VeloraPC.UI;

namespace VeloraPC.Views;

/// <summary>Storage Cleaner: scan, review every category and location, then clean. Personal files are never touched.</summary>
public sealed class CleanView : PageView
{
    static readonly string[] Colors = { "AccentBrush", "GoodBrush", "WarnBrush", "BadBrush", "MutedBrush" };

    readonly StackPanel host = new();
    List<CleanCategory> cats = new();
    CancellationTokenSource? cts;
    TextBlock? totalText;
    Border? barHost;
    Button? cleanBtn;
    bool active;

    public CleanView() : base("🧹 Storage Cleaner", "Scan first, review what's found, then clean. Personal files are never touched.")
    {
        Body.Children.Add(host);
    }

    public override void OnShown() { active = true; _ = ScanAsync(); }
    public override void OnHidden() { active = false; cts?.Cancel(); }

    void Show(UIElement e)
    {
        host.Children.Clear();
        host.Children.Add(e);
        Ui.FadeIn(e, 300, 10);
    }

    static StackPanel Working(string title, ProgressRow row, Action? cancel)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 30, 0, 0), MaxWidth = 560, HorizontalAlignment = HorizontalAlignment.Center };
        var t = Ui.Text(title, 20, true, "TextBrush", Ui.Display);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        t.Margin = new Thickness(0, 0, 0, 18);
        sp.Children.Add(t);
        sp.Children.Add(Ui.Card(row, 22, 16));
        if (cancel != null)
        {
            var b = Ui.Btn("CANCEL", cancel, "SecondaryButton", 130);
            b.HorizontalAlignment = HorizontalAlignment.Center;
            b.Margin = new Thickness(0, 18, 0, 0);
            sp.Children.Add(b);
        }
        return sp;
    }

    // ---------------- scan ----------------

    async Task ScanAsync()
    {
        cts?.Cancel();
        cts = new CancellationTokenSource();
        var token = cts.Token;

        var row = new ProgressRow(10);
        row.Reset("Getting ready…");
        Show(Working("Scanning your PC…", row, () => cts?.Cancel()));

        try
        {
            var progress = new Progress<TaskProgress>(p => row.Set(p.Fraction * 100, p.Message));
            cats = await Cleanup.ScanAsync(progress, token);
            row.Set(100, "Scan complete");
            if (active) RenderReady();
        }
        catch (OperationCanceledException) { if (active) RenderReady(); }
        catch (Exception ex)
        {
            Logger.Error("Scan failed", ex);
            if (active) Show(Ui.ErrorPanel(Friendly.Message(ex), () => _ = ScanAsync()));
        }
    }

    // ---------------- review ----------------

    void UpdateTotal()
    {
        long total = cats.Where(c => c.Selected).Sum(c => c.Bytes);
        if (totalText != null) totalText.Text = Fmt.Bytes(total);
        if (cleanBtn != null) cleanBtn.IsEnabled = cats.Any(c => c.Selected && c.Items > 0);
        if (barHost != null) barHost.Child = BuildBar();
    }

    Grid BuildBar()
    {
        var g = new Grid { Height = 10 };
        var shown = cats.Where(c => c.Selected && c.Bytes > 0).ToList();
        if (shown.Count == 0)
        {
            var empty = new Border { CornerRadius = new CornerRadius(5) };
            empty.SetResourceReference(Border.BackgroundProperty, "TrackBrush");
            g.Children.Add(empty);
            return g;
        }
        for (int i = 0; i < shown.Count; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(shown[i].Bytes, GridUnitType.Star) });
            var seg = new Border { CornerRadius = new CornerRadius(5), Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0) };
            seg.SetResourceReference(Border.BackgroundProperty, Colors[cats.IndexOf(shown[i]) % Colors.Length]);
            Grid.SetColumn(seg, i);
            g.Children.Add(seg);
        }
        return g;
    }

    void RenderReady()
    {
        var root = new StackPanel();

        var head = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        totalText = Ui.Text("", 46, true, "TextBrush", Ui.Display);
        head.Children.Add(totalText);
        head.Children.Add(Ui.Text("can be freed", 15, false, "MutedBrush"));
        root.Children.Add(head);

        barHost = new Border { Margin = new Thickness(0, 0, 0, 22) };
        root.Children.Add(barHost);

        int idx = 0;
        foreach (var cat in cats)
        {
            var c = cat;
            var color = Colors[idx++ % Colors.Length];

            var sw = new CheckBox { IsChecked = c.Selected, IsEnabled = c.Items > 0, Margin = new Thickness(0, 2, 16, 0), VerticalAlignment = VerticalAlignment.Top };
            sw.SetResourceReference(StyleProperty, "SwitchStyle");
            sw.Click += (_, _) => { c.Selected = sw.IsChecked == true; UpdateTotal(); };

            var info = new StackPanel();
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            var dot = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            dot.SetResourceReference(Border.BackgroundProperty, color);
            titleRow.Children.Add(dot);
            titleRow.Children.Add(Ui.Text(c.Name, 16, true));
            info.Children.Add(titleRow);
            var desc = Ui.Text(c.Description, 13, false, "MutedBrush");
            desc.Margin = new Thickness(0, 4, 0, 0);
            info.Children.Add(desc);
            if (c.Note.Length > 0)
            {
                var note = Ui.Text("ⓘ " + c.Note, 12.5, false, "WarnBrush");
                note.Margin = new Thickness(0, 4, 0, 0);
                info.Children.Add(note);
            }

            // review panel: where it is, how big, and the biggest files
            var review = new StackPanel { Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
            if (c.Locations.Count == 0)
                review.Children.Add(Ui.Text("Nothing to clean here.", 12, false, "MutedBrush"));
            foreach (var loc in c.Locations)
            {
                var l = Ui.Text($"{loc.FullPath}  —  {Fmt.Bytes(loc.Bytes)} · {loc.Items:N0} file(s)", 12, true, "MutedBrush");
                l.Margin = new Thickness(0, 6, 0, 2);
                review.Children.Add(l);
                foreach (var hit in loc.Top)
                {
                    var f = Ui.Text($"      {Path.GetFileName(hit.Path)}  ({Fmt.Bytes(hit.Bytes)})", 12, false, "MutedBrush");
                    f.TextTrimming = TextTrimming.CharacterEllipsis;
                    f.TextWrapping = TextWrapping.NoWrap;
                    review.Children.Add(f);
                }
            }
            info.Children.Add(review);

            var reviewBtn = Ui.Btn("Review items", () =>
                review.Visibility = review.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible, "GhostButton");
            reviewBtn.Padding = new Thickness(10, 4, 10, 4);
            reviewBtn.HorizontalAlignment = HorizontalAlignment.Left;
            reviewBtn.Margin = new Thickness(-10, 6, 0, 0);
            info.Children.Add(reviewBtn);

            var size = Ui.Text(Fmt.Bytes(c.Bytes), 20, true, "TextBrush", Ui.Display);
            size.VerticalAlignment = VerticalAlignment.Top;
            size.Margin = new Thickness(16, 0, 0, 0);

            var left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(sw);
            left.Children.Add(info);

            var card = Ui.Card(Ui.Row(left, size), 20, 16, hover: true);
            card.Margin = new Thickness(0, 0, 0, 12);
            root.Children.Add(card);
            Ui.FadeIn(card, 340, 14, idx * 70);
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        cleanBtn = Ui.Btn("CLEAN NOW", () => _ = CleanAsync(), "PrimaryButton", 180);
        cleanBtn.Margin = new Thickness(0, 0, 12, 0);
        actions.Children.Add(cleanBtn);
        actions.Children.Add(Ui.Btn("RESCAN", () => _ = ScanAsync(), "SecondaryButton", 140));
        root.Children.Add(actions);

        if (!Admin.IsAdmin)
        {
            var hint = Ui.Text("Windows' own temp and update folders are only included when Velora runs as administrator.", 12.5, false, "MutedBrush");
            hint.Margin = new Thickness(0, 16, 0, 0);
            root.Children.Add(hint);
            var more = Ui.Btn("Restart as administrator", () => _ = App.Shell.EnsureAdminAsync(
                "Cleaning Windows' own temp and update folders needs administrator rights."), "GhostButton");
            more.HorizontalAlignment = HorizontalAlignment.Left;
            more.Margin = new Thickness(-20, 0, 0, 0);
            root.Children.Add(more);
        }

        Show(root);
        UpdateTotal();
    }

    // ---------------- clean ----------------

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
        var token = cts.Token;

        var row = new ProgressRow(10);
        row.Reset("Getting ready…");
        Show(Working("Cleaning…", row, () => cts?.Cancel()));

        try
        {
            var progress = new Progress<TaskProgress>(p => row.Set(p.Fraction * 100, p.Message));
            var res = await Cleanup.CleanAsync(chosen, progress, token);
            row.Set(100, "Done");

            HistoryStore.Add("clean", "Storage Cleaner", $"{Fmt.Bytes(res.Bytes)} removed",
                $"Categories: {string.Join(", ", chosen.Select(c => c.Name))}\nFiles removed: {res.Files}\nSkipped (in use or protected): {res.Skipped}\nBlocked by permissions: {res.Denied}");
            if (active) ShowDone(res);
        }
        catch (OperationCanceledException)
        {
            if (active)
            {
                App.Shell.Toast("Cleaning cancelled. Anything already removed stays removed.", ToastKind.Warning);
                _ = ScanAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Clean failed", ex);
            if (active) Show(Ui.ErrorPanel(Friendly.Message(ex), () => _ = ScanAsync()));
        }
    }

    void ShowDone(CleanResult res)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 30, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 520 };
        var ok = Ui.Text("✓", 54, true, "GoodBrush"); ok.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(ok);
        var big = Ui.Text($"{Fmt.Bytes(res.Bytes)} cleaned", 34, true, "TextBrush", Ui.Display);
        big.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(big);

        string msg = res.Skipped == 0
            ? "Everything selected was removed."
            : $"{res.Skipped} file(s) were in use or protected and were left alone.";
        var sub = Ui.Text(msg, 14, false, "MutedBrush");
        sub.HorizontalAlignment = HorizontalAlignment.Center; sub.TextAlignment = TextAlignment.Center;
        sub.Margin = new Thickness(0, 6, 0, 22);
        sp.Children.Add(sub);

        if (res.Denied > 0 && !Admin.IsAdmin)
        {
            var d = Ui.Text($"{res.Denied} file(s) needed administrator permission.", 13, false, "WarnBrush");
            d.HorizontalAlignment = HorizontalAlignment.Center; d.Margin = new Thickness(0, -10, 0, 14);
            sp.Children.Add(d);
            var b2 = Ui.Btn("Restart as administrator", () => _ = App.Shell.EnsureAdminAsync(
                "Some files can only be cleaned with administrator rights."), "SecondaryButton");
            b2.HorizontalAlignment = HorizontalAlignment.Center; b2.Margin = new Thickness(0, 0, 0, 12);
            sp.Children.Add(b2);
        }

        var b = Ui.Btn("SCAN AGAIN", () => _ = ScanAsync(), "PrimaryButton", 170);
        b.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(b);
        Show(sp);
        App.Shell.Toast($"{Fmt.Bytes(res.Bytes)} cleaned", ToastKind.Success);
    }
}
