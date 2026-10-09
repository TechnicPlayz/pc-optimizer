using System.Windows;
using System.Windows.Controls;
using VeloraPC.Core;
using VeloraPC.UI;

namespace VeloraPC.Views;

public sealed class HistoryView : PageView
{
    readonly StackPanel host = new();

    public HistoryView() : base("🕘 History", "Everything Velora has done on this PC.")
    {
        Body.Children.Add(host);
        Render();
    }

    static string Emoji(string kind) => kind switch
    {
        "boost" => "🚀", "ram" => "⚡", "clean" => "🧹", "game" => "🎮", "repair" => "🔧", _ => "•"
    };

    static string DayLabel(DateTime d) =>
        d == DateTime.Today ? "Today" : d == DateTime.Today.AddDays(-1) ? "Yesterday" : d.ToString("dddd, dd MMM yyyy");

    void Render()
    {
        host.Children.Clear();
        var all = HistoryStore.All();

        if (all.Count == 0)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40, 0, 40) };
            var ic = Ui.Icon("🕘", 42); ic.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(ic);
            var t = Ui.Text("Nothing here yet", 18, true, "TextBrush", Ui.Display);
            t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new Thickness(0, 12, 0, 4);
            sp.Children.Add(t);
            var d = Ui.Text("Run a boost, clean or repair and it will show up here.", 13.5, false, "MutedBrush");
            d.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(d);
            var b = Ui.Btn("GO TO DASHBOARD", () => App.Shell.Navigate("dashboard"), "PrimaryButton", 190);
            b.Margin = new Thickness(0, 20, 0, 0);
            sp.Children.Add(b);
            host.Children.Add(Ui.Card(sp, 24, 18));
            return;
        }

        foreach (var group in all.GroupBy(x => x.TimeUtc.ToLocalTime().Date))
        {
            var h = Ui.Text(DayLabel(group.Key), 13, true, "MutedBrush");
            h.Margin = new Thickness(2, 14, 0, 8);
            host.Children.Add(h);

            foreach (var e in group)
            {
                var entry = e;
                var left = new StackPanel { Orientation = Orientation.Horizontal };
                var ic = Ui.Icon(Emoji(entry.Kind), 22); ic.Width = 40;
                left.Children.Add(ic);
                var txt = new StackPanel();
                txt.Children.Add(Ui.Text(entry.Title, 15, true));
                txt.Children.Add(Ui.Text(entry.Summary, 13, false, "MutedBrush"));
                left.Children.Add(txt);

                var details = Ui.LogBox(entry.Details);
                details.Visibility = Visibility.Collapsed;
                var time = Ui.Text(entry.TimeUtc.ToLocalTime().ToString("HH:mm"), 12.5, false, "MutedBrush");
                time.VerticalAlignment = VerticalAlignment.Center;

                var right = new StackPanel { Orientation = Orientation.Horizontal };
                right.Children.Add(time);
                if (!string.IsNullOrWhiteSpace(entry.Details))
                {
                    var tb = Ui.Btn("Details", () =>
                        details.Visibility = details.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible, "GhostButton");
                    tb.Margin = new Thickness(8, 0, 0, 0);
                    right.Children.Add(tb);
                }

                var stack = new StackPanel();
                stack.Children.Add(Ui.Row(left, right));
                details.Margin = new Thickness(40, 8, 0, 0);
                stack.Children.Add(details);

                var card = Ui.Card(stack, 16, 14);
                card.Margin = new Thickness(0, 0, 0, 8);
                host.Children.Add(card);
            }
        }

        var clear = Ui.Btn("CLEAR HISTORY", () => _ = ClearAsync(), "SecondaryButton", 170);
        clear.HorizontalAlignment = HorizontalAlignment.Left;
        clear.Margin = new Thickness(0, 18, 0, 0);
        host.Children.Add(clear);
    }

    async Task ClearAsync()
    {
        if (!await App.Shell.Confirm("Clear history?", "This removes the activity list. It doesn't undo anything on your PC.", "Clear", "Cancel")) return;
        HistoryStore.Clear();
        Render();
    }
}
