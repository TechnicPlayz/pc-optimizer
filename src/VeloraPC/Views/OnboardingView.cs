using System.Windows;
using System.Windows.Controls;
using VeloraPC.Core;
using VeloraPC.UI;

namespace VeloraPC.Views;

/// <summary>Short first-run intro: four slides, skippable at any time.</summary>
public sealed class OnboardingView : UserControl
{
    static readonly (string Emoji, string Title, string Text)[] Slides =
    {
        ("🩺", "PC Health", "See at a glance how your PC is doing: memory, storage, startup apps and Windows status. Read-only and honest."),
        ("🚀", "One-click Boost", "ULTRA ULTRA FAST PC cleans, tunes and checks your PC in one go, doing only what your PC actually needs."),
        ("🧹", "Cleanup you control", "Review exactly what will be removed before anything is deleted. Personal files are never touched."),
        ("🎮", "Gaming", "Turn on Game Mode and other safe gaming settings, and restore your original settings any time.")
    };

    readonly Action finish;
    readonly StackPanel slideHost = new();
    readonly StackPanel dots = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    readonly Button back, next;
    int index;

    public OnboardingView(Action onFinish)
    {
        finish = onFinish;
        var root = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Width = 520 };

        var welcome = Ui.Text($"Welcome to {Branding.Name}", 32, true, "TextBrush", Ui.Display);
        welcome.TextAlignment = TextAlignment.Center;
        root.Children.Add(welcome);
        var tag = Ui.Text(Branding.Tagline, 15.5, false, "MutedBrush");
        tag.TextAlignment = TextAlignment.Center; tag.Margin = new Thickness(0, 6, 0, 30);
        root.Children.Add(tag);

        root.Children.Add(Ui.Card(slideHost, 34, 22));
        dots.Margin = new Thickness(0, 22, 0, 22);
        root.Children.Add(dots);

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        back = Ui.Btn("BACK", () => Go(index - 1), "SecondaryButton", 120);
        back.Margin = new Thickness(0, 0, 12, 0);
        next = Ui.Btn("NEXT", () => { if (index >= Slides.Length - 1) finish(); else Go(index + 1); }, "PrimaryButton", 220);
        row.Children.Add(back); row.Children.Add(next);
        root.Children.Add(row);

        var skip = Ui.Btn("Skip", () => finish(), "GhostButton");
        skip.HorizontalAlignment = HorizontalAlignment.Center; skip.Margin = new Thickness(0, 10, 0, 0);
        root.Children.Add(skip);

        Content = root;
        Go(0);
    }

    void Go(int i)
    {
        index = Math.Clamp(i, 0, Slides.Length - 1);
        var (emoji, title, text) = Slides[index];

        slideHost.Children.Clear();
        var ic = Ui.Icon(emoji, 48); ic.HorizontalAlignment = HorizontalAlignment.Center;
        slideHost.Children.Add(ic);
        var t = Ui.Text(title, 22, true, "TextBrush", Ui.Display);
        t.HorizontalAlignment = HorizontalAlignment.Center; t.Margin = new Thickness(0, 14, 0, 8);
        slideHost.Children.Add(t);
        var d = Ui.Text(text, 14.5, false, "MutedBrush");
        d.TextAlignment = TextAlignment.Center;
        slideHost.Children.Add(d);
        Ui.FadeIn(slideHost, 220, 8);

        dots.Children.Clear();
        for (int k = 0; k < Slides.Length; k++)
        {
            var dot = new Border { Width = k == index ? 22 : 8, Height = 8, CornerRadius = new CornerRadius(4), Margin = new Thickness(4, 0, 4, 0) };
            dot.SetResourceReference(Border.BackgroundProperty, k == index ? "AccentBrush" : "TrackBrush");
            dots.Children.Add(dot);
        }

        back.Visibility = index == 0 ? Visibility.Collapsed : Visibility.Visible;
        next.Content = index == Slides.Length - 1 ? "LET'S GET STARTED" : "NEXT";
    }
}
