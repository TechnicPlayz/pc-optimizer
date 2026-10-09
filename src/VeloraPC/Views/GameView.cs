using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using VeloraPC.Core;
using VeloraPC.Services;
using VeloraPC.UI;

namespace VeloraPC.Views;

public sealed class GameView : PageView
{
    readonly StackPanel host = new();
    bool highPerf;

    public GameView() : base("🎮 Game Boost", "Safe Windows settings for smoother gaming. Every change can be undone.")
    {
        Body.Children.Add(host);
    }

    public override void OnShown() => _ = LoadAsync();

    async Task LoadAsync()
    {
        host.Children.Clear();
        var s = Ui.Spinner(36); s.HorizontalAlignment = HorizontalAlignment.Center; s.Margin = new Thickness(0, 40, 0, 0);
        host.Children.Add(s);
        try
        {
            var st = await GameService.GetStatusAsync();
            var apps = await Task.Run(() => GameService.BackgroundApps());
            Render(st, apps);
        }
        catch (Exception ex)
        {
            Logger.Error("Game status failed", ex);
            host.Children.Clear();
            host.Children.Add(Ui.ErrorPanel(Friendly.Message(ex), () => _ = LoadAsync()));
        }
    }

    static Border StatusCard(string label, string value, string brush)
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.Text(label.ToUpperInvariant(), 11.5, true, "MutedBrush"));
        var v = Ui.Text(value, 22, true, brush, Ui.Display);
        v.Margin = new Thickness(0, 8, 0, 0);
        sp.Children.Add(v);
        var c = Ui.Card(sp, 20, 16);
        c.Margin = new Thickness(6);
        return c;
    }

    void Render(GameStatus st, List<BackgroundApp> apps)
    {
        host.Children.Clear();
        var root = new StackPanel();

        var grid = new UniformGrid { Columns = 3, Margin = new Thickness(-6, 0, -6, 0) };
        grid.Children.Add(StatusCard("Game Mode", st.GameMode ? "ON" : "OFF", st.GameMode ? "GoodBrush" : "WarnBrush"));
        grid.Children.Add(StatusCard("Background capture", st.Capture ? "ON" : "OFF", st.Capture ? "WarnBrush" : "GoodBrush"));
        grid.Children.Add(StatusCard("Power plan", st.SchemeName, "TextBrush"));
        root.Children.Add(grid);

        // Options
        var opts = new StackPanel();
        opts.Children.Add(Ui.Text("Recommended settings", 16, true, "TextBrush", Ui.Display));
        var recs = Ui.Text("Turns Game Mode on, turns off Game Bar background recording, and moves you off Power saver while plugged in. " +
                           "Your original settings are saved first.", 13, false, "MutedBrush");
        recs.Margin = new Thickness(0, 6, 0, 16);
        opts.Children.Add(recs);

        var hpSwitch = new CheckBox { IsChecked = highPerf };
        hpSwitch.SetResourceReference(StyleProperty, "SwitchStyle");
        hpSwitch.Click += (_, _) => highPerf = hpSwitch.IsChecked == true;
        var hpText = new StackPanel();
        hpText.Children.Add(Ui.Text("Use High performance while plugged in", 14, true));
        hpText.Children.Add(Ui.Text("Optional. Uses more power and runs warmer — best for desktops and laptops on charger.", 12.5, false, "MutedBrush"));
        opts.Children.Add(Ui.Row(hpText, hpSwitch));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 22, 0, 0) };
        var apply = Ui.Btn("APPLY RECOMMENDED", () => _ = ApplyAsync(), "PrimaryButton", 210);
        apply.Margin = new Thickness(0, 0, 12, 0);
        buttons.Children.Add(apply);
        var restore = Ui.Btn("RESTORE ORIGINAL", () => _ = RestoreAsync(), "SecondaryButton", 190);
        restore.IsEnabled = GameService.HasSnapshot;
        buttons.Children.Add(restore);
        opts.Children.Add(buttons);
        var card = Ui.Card(opts, 24, 18);
        card.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(card);

        // Background app recommendations
        var bg = new StackPanel();
        bg.Children.Add(Ui.Text("Background app recommendations", 16, true, "TextBrush", Ui.Display));
        if (apps.Count == 0)
        {
            var none = Ui.Text("No heavy background apps right now.", 13.5, false, "MutedBrush");
            none.Margin = new Thickness(0, 8, 0, 0);
            bg.Children.Add(none);
        }
        else
        {
            var d = Ui.Text("These apps use a lot of memory. Close any you don't need before gaming. Velora never closes apps for you.", 13, false, "MutedBrush");
            d.Margin = new Thickness(0, 6, 0, 10);
            bg.Children.Add(d);
            foreach (var a in apps)
            {
                var row = Ui.Row(Ui.Text(a.Name, 14), Ui.Text(Fmt.Bytes(a.Bytes), 14, true, "MutedBrush"));
                row.Margin = new Thickness(0, 5, 0, 5);
                bg.Children.Add(row);
            }
        }
        var bgCard = Ui.Card(bg, 24, 18);
        bgCard.Margin = new Thickness(0, 14, 0, 0);
        root.Children.Add(bgCard);

        host.Children.Add(root);
        Ui.FadeIn(root, 240, 8);
    }

    async Task ApplyAsync()
    {
        try
        {
            var changes = await GameService.ApplyAsync(new GameOptions(true, true, true, highPerf));
            if (changes.Count == 0) App.Shell.Toast("Everything was already optimized.", ToastKind.Info);
            else
            {
                HistoryStore.Add("game", "Game Boost", string.Join(", ", changes), string.Join("\n", changes));
                App.Shell.Toast("Gaming settings applied.", ToastKind.Success);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Game apply failed", ex);
            App.Shell.Toast(Friendly.Message(ex), ToastKind.Error);
        }
        await LoadAsync();
    }

    async Task RestoreAsync()
    {
        bool ok = await App.Shell.Confirm("Restore original settings?",
            "Game Mode, background capture and your power plan will go back to how they were before Velora changed them.",
            "Restore", "Cancel");
        if (!ok) return;
        try
        {
            await GameService.RevertAsync();
            HistoryStore.Add("game", "Game Boost", "Original settings restored");
            App.Shell.Toast("Original settings restored.", ToastKind.Success);
        }
        catch (Exception ex)
        {
            Logger.Error("Game revert failed", ex);
            App.Shell.Toast(Friendly.Message(ex), ToastKind.Error);
        }
        await LoadAsync();
    }
}
