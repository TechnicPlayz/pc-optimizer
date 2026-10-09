using System.Windows;
using System.Windows.Controls;
using VeloraPC.Core;
using VeloraPC.Services;
using VeloraPC.UI;

namespace VeloraPC.Views;

public sealed class HealthView : PageView
{
    readonly StackPanel host = new();

    public HealthView() : base("🩺 PC Health", "A read-only look at how your PC is doing. Nothing here changes your system.")
    {
        Body.Children.Add(host);
        Render();
    }

    void Render()
    {
        host.Children.Clear();
        List<HealthCheck> checks;
        try { checks = HealthService.Evaluate(); }
        catch (Exception ex)
        {
            Logger.Error("Health evaluation failed", ex);
            host.Children.Add(Ui.ErrorPanel(Friendly.Message(ex), Render));
            return;
        }

        var overall = HealthService.Overall(checks);
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 18) };
        var summary = overall == HealthStatus.Good ? "Everything looks good."
                    : overall == HealthStatus.Attention ? "A few things could use attention."
                    : "Some things need your attention.";
        var s = Ui.Text(summary, 20, true, "TextBrush", Ui.Display);
        s.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(s);
        var pill = Ui.Pill(HealthService.Label(overall), HealthService.BrushKey(overall));
        pill.Margin = new Thickness(14, 0, 0, 0);
        head.Children.Add(pill);
        host.Children.Add(head);

        int i = 0;
        foreach (var c in checks)
        {
            var left = new StackPanel { Orientation = Orientation.Horizontal };
            var ic = Ui.Icon(c.Emoji, 22); ic.Width = 40;
            left.Children.Add(ic);
            var txt = new StackPanel();
            txt.Children.Add(Ui.Text(c.Name, 15, true));
            txt.Children.Add(Ui.Text(c.Detail, 13, false, "MutedBrush"));
            left.Children.Add(txt);

            var card = Ui.Card(Ui.Row(left, Ui.Pill(HealthService.Label(c.Status), HealthService.BrushKey(c.Status))), 18, 14);
            card.Margin = new Thickness(0, 0, 0, 10);
            host.Children.Add(card);
            Ui.FadeIn(card, 220, 8, i++ * 40);
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var r = Ui.Btn("REFRESH", Render, "SecondaryButton", 140);
        r.Margin = new Thickness(0, 0, 12, 0);
        actions.Children.Add(r);
        actions.Children.Add(Ui.Btn("OPEN SYSTEM REPAIR", () => App.Shell.Navigate("repair"), "GhostButton"));
        host.Children.Add(actions);
    }
}
