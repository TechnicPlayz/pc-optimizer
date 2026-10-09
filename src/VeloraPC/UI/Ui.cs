using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VeloraPC.Core;

namespace VeloraPC.UI;

public enum ToastKind { Info, Success, Warning, Error }

public interface IShell
{
    void Navigate(string key);
    void StartBoost();
    void Toast(string message, ToastKind kind = ToastKind.Info);
    Task<bool> Confirm(string title, string message, string yes = "Continue", string no = "Cancel");
    Task Info(string title, string message, string ok = "OK");
    Task<bool> EnsureAdminAsync(string reason);
    void RefreshTheme();
}

public static class ThemeService
{
    public static bool IsDark { get; private set; } = true;

    static bool SystemPrefersDark()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return !(k?.GetValue("AppsUseLightTheme") is int v && v == 1);
        }
        catch { return true; }
    }

    public static void Apply(string mode)
    {
        bool dark = mode == "Dark" || (mode != "Light" && SystemPrefersDark());
        IsDark = dark;
        var dict = new ResourceDictionary
        {
            Source = new Uri($"/VeloraPC;component/Themes/{(dark ? "Dark" : "Light")}.xaml", UriKind.Relative)
        };
        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count > 0) merged[0] = dict; else merged.Insert(0, dict);
    }
}

/// <summary>Small design-system toolkit: every screen is built from these so the app stays consistent.</summary>
public static class Ui
{
    public static readonly FontFamily BodyFont = new("Segoe UI Variable Text, Segoe UI");
    public static readonly FontFamily Display = new("Segoe UI Variable Display, Segoe UI");
    public static readonly FontFamily Emoji = new("Segoe UI Emoji");

    public static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

    public static TextBlock Text(string text, double size = 14, bool bold = false,
        string brush = "TextBrush", FontFamily? font = null)
    {
        var t = new TextBlock
        {
            Text = text, FontSize = size, FontFamily = font ?? BodyFont,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal
        };
        t.SetResourceReference(TextBlock.ForegroundProperty, brush);
        return t;
    }

    public static void Fg(TextBlock t, string brush) => t.SetResourceReference(TextBlock.ForegroundProperty, brush);

    public static TextBlock Icon(string emoji, double size) => new()
    {
        Text = emoji, FontSize = size, FontFamily = Emoji, VerticalAlignment = VerticalAlignment.Center
    };

    public static Border Card(UIElement child, double pad = 20, double radius = 16,
        string bg = "CardBrush", string border = "BorderBrush")
    {
        var b = new Border
        {
            Child = child, Padding = new Thickness(pad), CornerRadius = new CornerRadius(radius),
            BorderThickness = new Thickness(1)
        };
        b.SetResourceReference(Border.BackgroundProperty, bg);
        b.SetResourceReference(Border.BorderBrushProperty, border);
        return b;
    }

    public static Button Btn(string text, Action onClick, string style = "PrimaryButton", double width = double.NaN)
    {
        var b = new Button { Content = text, Width = width };
        b.SetResourceReference(FrameworkElement.StyleProperty, style);
        b.Click += (_, _) => onClick();
        return b;
    }

    public static Border Pill(string text, string brushKey)
    {
        var t = Text(text, 11, true, brushKey);
        t.TextWrapping = TextWrapping.NoWrap;
        var b = new Border
        {
            Child = t, Padding = new Thickness(10, 3, 10, 3),
            CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center
        };
        b.SetResourceReference(Border.BorderBrushProperty, brushKey);
        return b;
    }

    public static ProgressBar Bar(double max = 100)
    {
        var p = new ProgressBar { Minimum = 0, Maximum = max, Height = 6 };
        p.SetResourceReference(FrameworkElement.StyleProperty, "FlatProgress");
        return p;
    }

    public static void SmoothTo(ProgressBar p, double value, double ms = 350)
    {
        p.BeginAnimation(ProgressBar.ValueProperty, new DoubleAnimation(value, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    public static FrameworkElement Gap(double h) => new Border { Height = h };

    public static TextBox LogBox(string text)
    {
        var tb = new TextBox
        {
            Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0),
            Background = Brushes.Transparent, FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = 12,
            MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        tb.SetResourceReference(Control.ForegroundProperty, "MutedBrush");
        return tb;
    }

    public static void FadeIn(UIElement e, double ms = 240, double dy = 10, double delayMs = 0)
    {
        e.Opacity = 0;
        var tt = new TranslateTransform(0, dy);
        e.RenderTransform = tt;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var begin = TimeSpan.FromMilliseconds(delayMs);
        e.BeginAnimation(UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(ms)) { BeginTime = begin, EasingFunction = ease });
        tt.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(dy, 0, TimeSpan.FromMilliseconds(ms + 60)) { BeginTime = begin, EasingFunction = ease });
    }

    /// <summary>Honest indeterminate indicator for work whose length is unknown (never shows a fake percentage).</summary>
    public static FrameworkElement Spinner(double size = 28)
    {
        var c = new CircularProgress
        {
            Width = size, Height = size, Thickness = Math.Max(2.5, size / 9), Value = 0.28,
            RenderTransformOrigin = new Point(0.5, 0.5)
        };
        var rt = new RotateTransform();
        c.RenderTransform = rt;
        rt.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.9)) { RepeatBehavior = RepeatBehavior.Forever });
        return c;
    }

    public static Border ErrorPanel(string message, Action retry)
    {
        var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var icon = Icon("⚠️", 34); icon.HorizontalAlignment = HorizontalAlignment.Center;
        sp.Children.Add(icon);
        var h = Text("We couldn't complete this operation.", 18, true, "TextBrush", Display);
        h.TextAlignment = TextAlignment.Center; h.Margin = new Thickness(0, 12, 0, 6);
        sp.Children.Add(h);
        var m = Text(message, 14, false, "MutedBrush"); m.TextAlignment = TextAlignment.Center; m.MaxWidth = 420;
        sp.Children.Add(m);
        var b = Btn("TRY AGAIN", retry, "PrimaryButton", 150);
        b.Margin = new Thickness(0, 18, 0, 0);
        sp.Children.Add(b);
        return Card(sp, 32, 18);
    }

    public static Grid Row(UIElement left, UIElement right)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(right, 1);
        g.Children.Add(left); g.Children.Add(right);
        return g;
    }
}

/// <summary>Circular progress ring (determinate) — also used rotating as a spinner.</summary>
public sealed class CircularProgress : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(CircularProgress),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Thickness { get; set; } = 12;

    protected override void OnRender(DrawingContext dc)
    {
        double size = Math.Min(ActualWidth, ActualHeight);
        if (size <= Thickness) return;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        double r = size / 2 - Thickness / 2;

        var track = TryFindResource("TrackBrush") as Brush ?? Brushes.Gray;
        var accent = TryFindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue;
        dc.DrawEllipse(null, new Pen(track, Thickness), c, r, r);

        double v = Math.Clamp(Value, 0, 1);
        if (v <= 0.001) return;
        var pen = new Pen(accent, Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (v >= 0.999) { dc.DrawEllipse(null, pen, c, r, r); return; }

        double ang = v * 2 * Math.PI;
        var start = new Point(c.X, c.Y - r);
        var end = new Point(c.X + r * Math.Sin(ang), c.Y - r * Math.Cos(ang));
        var fig = new PathFigure { StartPoint = start, IsClosed = false };
        fig.Segments.Add(new ArcSegment(end, new Size(r, r), 0, ang > Math.PI, SweepDirection.Clockwise, true));
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        dc.DrawGeometry(null, pen, geo);
    }
}

/// <summary>Base for every screen: consistent header, scrolling, spacing and lifecycle hooks.</summary>
public abstract class PageView : UserControl
{
    protected readonly StackPanel Body = new();

    protected PageView(string? title, string? subtitle)
    {
        var root = new StackPanel { Margin = new Thickness(40, 32, 40, 40), MaxWidth = 1000 };
        if (!string.IsNullOrEmpty(title))
        {
            root.Children.Add(Ui.Text(title, 30, true, "TextBrush", Ui.Display));
            if (!string.IsNullOrEmpty(subtitle))
            {
                var s = Ui.Text(subtitle, 14.5, false, "MutedBrush");
                s.Margin = new Thickness(0, 4, 0, 0);
                root.Children.Add(s);
            }
            Body.Margin = new Thickness(0, 24, 0, 0);
        }
        root.Children.Add(Body);
        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = root
        };
    }

    public virtual void OnShown() { }
    public virtual void OnHidden() { }
}
