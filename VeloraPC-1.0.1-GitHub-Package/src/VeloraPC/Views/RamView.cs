using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using VeloraPC.Core;
using VeloraPC.Services;
using VeloraPC.UI;

namespace VeloraPC.Views;

public sealed class RamView : PageView
{
    readonly TextBlock nowVal = Ui.Text("—", 32, true, "TextBrush", Ui.Display);
    readonly TextBlock nowSub = Ui.Text("", 13, false, "MutedBrush");
    readonly ProgressBar bar = Ui.Bar();
    readonly Button boost;
    readonly StackPanel resultHost = new() { Margin = new Thickness(0, 18, 0, 0) };
    CancellationTokenSource? cts;
    DispatcherTimer? timer;
    bool running;

    public RamView() : base("⚡ RAM Boost", "Free available memory and reduce unnecessary memory usage.")
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.Text("MEMORY RIGHT NOW", 11.5, true, "MutedBrush"));
        nowVal.Margin = new Thickness(0, 6, 0, 2);
        sp.Children.Add(nowVal);
        sp.Children.Add(nowSub);
        bar.Margin = new Thickness(0, 16, 0, 20);
        sp.Children.Add(bar);

        boost = Ui.Btn("BOOST RAM", () => _ = RunAsync(), "PrimaryButton", 180);
        boost.HorizontalAlignment = HorizontalAlignment.Left;
        sp.Children.Add(boost);
        var card = Ui.Card(sp, 26, 18, hover: true);
        Body.Children.Add(card);
        Ui.FadeIn(card, 340, 14);
        Body.Children.Add(resultHost);

        var note = Ui.Text("RAM Boost can't add physical memory. It asks Windows to release memory that apps are holding but " +
                           "aren't actively using, so it can be reused. Apps may reload some of it as you keep working, so the " +
                           "effect is temporary. Nothing is closed and nothing is deleted.", 12.5, false, "MutedBrush");
        note.Margin = new Thickness(0, 18, 0, 0);
        Body.Children.Add(note);
        Refresh();
    }

    void Refresh()
    {
        var m = SystemInfo.Memory();
        nowVal.Text = $"{Fmt.Gb(m.Used)} / {m.Total / 1073741824.0:0} GB";
        nowSub.Text = $"{m.Percent:0}% in use · {Fmt.Gb(m.Avail)} GB available";
        Ui.SmoothTo(bar, m.Percent, 300);
    }

    public override void OnShown()
    {
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (_, _) => { if (!running) Refresh(); };
        timer.Start();
    }

    public override void OnHidden() { timer?.Stop(); cts?.Cancel(); }

    async Task RunAsync()
    {
        if (running) return;
        running = true;
        boost.IsEnabled = false;
        boost.Content = "Optimizing…";
        resultHost.Children.Clear();

        var row = new ProgressRow(10);
        row.Reset("Getting ready…");
        var card = Ui.Card(row, 22, 16);
        resultHost.Children.Add(card);
        Ui.FadeIn(card, 260, 8);

        cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<TaskProgress>(p => row.Set(p.Fraction * 100, p.Message));
            var res = await MemoryService.BoostAsync(cts.Token, progress);
            row.Set(100, "Done");
            await Task.Delay(250);
            ShowResult(res);
            HistoryStore.Add("ram", "RAM Boost",
                res.Freed > 0 ? $"{Fmt.Bytes(res.Freed)} freed" : "Memory was already well balanced",
                $"Used before: {Fmt.Bytes(res.UsedBefore)}\nUsed after: {Fmt.Bytes(res.UsedAfter)}\nProcesses trimmed: {res.Trimmed}");
        }
        catch (OperationCanceledException) { resultHost.Children.Clear(); }
        catch (Exception ex)
        {
            Logger.Error("RAM boost failed", ex);
            resultHost.Children.Clear();
            resultHost.Children.Add(Ui.ErrorPanel(Friendly.Message(ex), () => _ = RunAsync()));
        }
        finally
        {
            running = false;
            boost.IsEnabled = true;
            boost.Content = "BOOST RAM";
            Refresh();
        }
    }

    void ShowResult(MemoryBoostResult r)
    {
        resultHost.Children.Clear();
        var g = new UniformGrid { Columns = 2, Margin = new Thickness(-6, 0, -6, 0) };
        var before = Compare("BEFORE", $"{Fmt.Gb(r.UsedBefore)} GB used", "MutedBrush");
        var after = Compare("AFTER", $"{Fmt.Gb(r.UsedAfter)} GB used", r.UsedAfter < r.UsedBefore ? "GoodBrush" : "TextBrush");
        g.Children.Add(before);
        g.Children.Add(after);
        resultHost.Children.Add(g);
        Ui.FadeIn(before, 340, 16);
        Ui.FadeIn(after, 340, 16, 120);

        string msg = r.Freed > 50L * 1024 * 1024
            ? $"{Fmt.Bytes(r.Freed)} of memory is now available (measured across {r.Trimmed} apps)."
            : "Your memory was already well balanced, so there wasn't much to free.";
        var t = Ui.Text(msg, 14, true);
        t.Margin = new Thickness(0, 12, 0, 0);
        resultHost.Children.Add(t);
        Ui.FadeIn(t, 300, 8, 240);
    }

    static Border Compare(string label, string value, string brush)
    {
        var sp = new StackPanel();
        sp.Children.Add(Ui.Text(label, 11.5, true, "MutedBrush"));
        var v = Ui.Text(value, 24, true, brush, Ui.Display);
        v.Margin = new Thickness(0, 6, 0, 0);
        sp.Children.Add(v);
        var c = Ui.Card(sp, 20, 16, hover: true);
        c.Margin = new Thickness(6);
        return c;
    }
}
