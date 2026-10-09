using System.Windows;
using System.Windows.Controls;
using VeloraPC.Core;
using VeloraPC.Services;
using VeloraPC.UI;

namespace VeloraPC.Views;

public sealed class RepairView : PageView
{
    sealed class CardParts
    {
        public ProgressBar Bar = Ui.Bar();
        public TextBlock Status = Ui.Text("", 13, false, "MutedBrush");
        public Button Run = null!;
        public StackPanel Details = new() { Visibility = Visibility.Collapsed };
        public Button Toggle = null!;
    }

    readonly Dictionary<RepairKind, CardParts> parts = new();
    CancellationTokenSource? cts;
    RepairKind? running;

    public RepairView() : base("🔧 System Repair", "Check and repair Windows itself. These tools make no changes unless you run a repair.")
    {
        if (!Admin.IsAdmin)
        {
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text("Administrator access is required", 15, true));
            var d = Ui.Text("Windows only lets these tools run with administrator rights. Velora asks once, and only when you start a tool.", 13, false, "MutedBrush");
            d.Margin = new Thickness(0, 4, 0, 12);
            sp.Children.Add(d);
            var b = Ui.Btn("Restart as administrator", () => _ = App.Shell.EnsureAdminAsync(
                "System repair tools can only run with administrator rights."), "SecondaryButton");
            b.HorizontalAlignment = HorizontalAlignment.Left;
            sp.Children.Add(b);
            var banner = Ui.Card(sp, 20, 16);
            banner.Margin = new Thickness(0, 0, 0, 16);
            Body.Children.Add(banner);
        }

        foreach (var kind in new[] { RepairKind.DismCheck, RepairKind.DismScan, RepairKind.DismRestore, RepairKind.Sfc })
            Body.Children.Add(BuildCard(kind));
    }

    Border BuildCard(RepairKind kind)
    {
        var p = new CardParts();
        parts[kind] = p;

        var info = new StackPanel();
        info.Children.Add(Ui.Text(RepairService.Title(kind), 16, true));
        var ex = Ui.Text(RepairService.Explain(kind), 13, false, "MutedBrush");
        ex.Margin = new Thickness(0, 4, 0, 0);
        info.Children.Add(ex);

        p.Bar.Margin = new Thickness(0, 14, 0, 0);
        p.Bar.Visibility = Visibility.Collapsed;
        info.Children.Add(p.Bar);
        p.Status.Margin = new Thickness(0, 10, 0, 0);
        info.Children.Add(p.Status);
        p.Toggle = Ui.Btn("View details", () =>
            p.Details.Visibility = p.Details.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible, "GhostButton");
        p.Toggle.HorizontalAlignment = HorizontalAlignment.Left;
        p.Toggle.Margin = new Thickness(-18, 4, 0, 0);
        p.Toggle.Visibility = Visibility.Collapsed;
        info.Children.Add(p.Toggle);
        info.Children.Add(p.Details);

        var k = kind;
        p.Run = Ui.Btn("RUN", () => _ = ToggleAsync(k), "PrimaryButton", 120);
        p.Run.VerticalAlignment = VerticalAlignment.Top;
        p.Run.Margin = new Thickness(20, 0, 0, 0);

        var card = Ui.Card(Ui.Row(info, p.Run), 22, 16);
        card.Margin = new Thickness(0, 0, 0, 12);
        return card;
    }

    public override void OnHidden() => cts?.Cancel();

    async Task ToggleAsync(RepairKind kind)
    {
        if (running == kind) { cts?.Cancel(); return; }
        if (running != null) { App.Shell.Toast("Another repair tool is already running.", ToastKind.Warning); return; }
        if (!await App.Shell.EnsureAdminAsync("System repair tools can only run with administrator rights.")) return;

        running = kind;
        cts = new CancellationTokenSource();
        var p = parts[kind];
        foreach (var kv in parts) if (kv.Key != kind) kv.Value.Run.IsEnabled = false;
        p.Run.Content = "CANCEL";
        p.Run.SetResourceReference(StyleProperty, "SecondaryButton");
        p.Bar.Visibility = Visibility.Visible;
        p.Bar.BeginAnimation(ProgressBar.ValueProperty, null);
        p.Bar.Value = 0;
        p.Details.Visibility = Visibility.Collapsed;
        p.Details.Children.Clear();
        p.Toggle.Visibility = Visibility.Collapsed;
        p.Status.Text = "Running… this can take a while. You can keep using your PC.";
        Ui.Fg(p.Status, "MutedBrush");

        try
        {
            var progress = new Progress<double>(v => Ui.SmoothTo(p.Bar, v, 250));
            var res = await RepairService.RunAsync(kind, progress, cts.Token);

            p.Status.Text = res.Summary;
            Ui.Fg(p.Status, res.Outcome switch
            {
                RepairOutcome.Ok => "GoodBrush",
                RepairOutcome.Attention => "WarnBrush",
                RepairOutcome.Failed => "BadBrush",
                _ => "MutedBrush"
            });
            if (res.Outcome != RepairOutcome.Cancelled)
            {
                Ui.SmoothTo(p.Bar, 100, 200);
                p.Details.Children.Add(Ui.LogBox(res.Output));
                p.Toggle.Visibility = Visibility.Visible;

                HistoryStore.Add("repair", RepairService.Title(kind), res.Summary, res.Output);
                App.Shell.Toast(res.Summary, res.Outcome == RepairOutcome.Ok ? ToastKind.Success : ToastKind.Warning);
            }
            else
            {
                p.Bar.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Repair failed", ex);
            p.Status.Text = "We couldn't complete this operation. " + Friendly.Message(ex);
            Ui.Fg(p.Status, "BadBrush");
        }
        finally
        {
            running = null;
            foreach (var kv in parts) kv.Value.Run.IsEnabled = true;
            p.Run.Content = "RUN";
            p.Run.SetResourceReference(StyleProperty, "PrimaryButton");
        }
    }
}
