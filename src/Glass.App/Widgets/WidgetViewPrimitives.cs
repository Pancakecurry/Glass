using System.Globalization;
using Glass.Core.Applications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Glass.App.Widgets;

internal static class WidgetViewPrimitives
{
    public static StackPanel Panel(string title)
    {
        var panel = new StackPanel { Spacing = 8, Padding = new Thickness(12) };
        if (!string.IsNullOrEmpty(title)) panel.Children.Add(new TextBlock
        {
            Text = title,
            Style = Application.Current.Resources["GlassSectionTitleStyle"] as Style,
        });
        return panel;
    }
    public static StackPanel Metric(string title, string caption, out TextBlock value)
    {
        var panel = Panel(title);
        value = Display("—");
        panel.Children.Add(value);
        panel.Children.Add(Caption(caption));
        return panel;
    }
    public static TextBlock Value(string text) => new()
    {
        Text = text, FontSize = 16,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
    public static TextBlock Display(string text) => new()
    {
        Text = text, FontSize = 30,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };
    public static TextBlock Caption(string text) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap, Opacity = 0.72, FontSize = 12,
    };
    public static Button GlyphButton(string glyph, string name)
    {
        var button = new Button
        {
            Content = new FontIcon { Glyph = glyph }, MinWidth = 36, MinHeight = 36,
            Padding = new Thickness(6),
        };
        AutomationProperties.SetName(button, name);
        ToolTipService.SetToolTip(button, name);
        return button;
    }
    public static Button AsyncAction(string glyph, string name, Func<Task> action)
    {
        var button = GlyphButton(glyph, name);
        button.Click += async (_, _) => await action();
        return button;
    }
    public static Button TextAction(string text, Action action)
    {
        var button = new Button { Content = text, MinHeight = 36 };
        button.Click += (_, _) => action();
        return button;
    }
    public static StackPanel Actions(params UIElement[] controls)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var control in controls) panel.Children.Add(control);
        return panel;
    }
    public static FrameworkElement Unavailable(string title, string detail)
    {
        var panel = Panel(title);
        panel.Children.Add(Caption(detail));
        return panel;
    }
    public static string FormatTime(TimeSpan value, bool tenths = false) => tenths
        ? $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds / 100}"
        : $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}";
    public static string FormatRate(long bytes) => bytes switch
    {
        >= 1_000_000 => $"{bytes / 1_000_000d:0.0} MB/s",
        >= 1_000 => $"{bytes / 1_000d:0.0} KB/s",
        _ => $"{bytes} B/s",
    };
    public static double ReadNumber(
        IReadOnlyDictionary<string, string> settings, string key, double fallback) =>
        settings.TryGetValue(key, out var value) &&
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : fallback;
    public static bool TryParseIdentity(string value, out ApplicationIdentity identity)
    {
        var separator = value.IndexOf(':');
        if (separator > 0 && Enum.TryParse<ApplicationIdentityKind>(
            value[..separator], out var kind))
        {
            identity = new ApplicationIdentity(kind, value[(separator + 1)..]);
            return identity.IsValid;
        }
        identity = default;
        return false;
    }
}

internal sealed class ActiveRefreshController(
    FrameworkElement owner,
    TimeSpan interval,
    Func<bool> shouldRun,
    Action refresh) : IDisposable
{
    private readonly DispatcherTimer _timer = new() { Interval = interval };
    private bool _loaded;

    public void Attach()
    {
        _timer.Tick += OnTick;
        owner.Loaded += OnLoaded;
        owner.Unloaded += OnUnloaded;
    }
    public void NotifyStateChanged()
    {
        refresh();
        if (_loaded && shouldRun()) _timer.Start();
        else _timer.Stop();
    }
    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        owner.Loaded -= OnLoaded;
        owner.Unloaded -= OnUnloaded;
    }
    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _loaded = true;
        NotifyStateChanged();
    }
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _loaded = false;
        _timer.Stop();
    }
    private void OnTick(object? sender, object args) => NotifyStateChanged();
}
