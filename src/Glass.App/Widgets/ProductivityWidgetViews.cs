using System.Globalization;
using Glass.Widgets.BuiltIn;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using static Glass.App.Widgets.WidgetViewPrimitives;

namespace Glass.App.Widgets;

internal sealed class ProductivityWidgetViews
{
    public FrameworkElement Calendar(CalendarWidgetInstance instance)
    {
        var panel = Panel("Calendar");
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var month = Value(string.Empty);
        var previous = GlyphButton("\uE76B", "Previous month");
        var next = GlyphButton("\uE76C", "Next month");
        var today = new Button { Content = "Today", MinHeight = 36 };
        previous.Click += (_, _) => instance.Previous();
        next.Click += (_, _) => instance.Next();
        today.Click += (_, _) => instance.Today();
        header.Children.Add(previous); header.Children.Add(month);
        header.Children.Add(next); header.Children.Add(today);
        var grid = new Grid();
        var selectedDate = Caption("No date selected");
        for (var index = 0; index < 7; index++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var index = 0; index < 6; index++) grid.RowDefinitions.Add(new RowDefinition());
        panel.Children.Add(header); panel.Children.Add(selectedDate); panel.Children.Add(grid);
        void Refresh()
        {
            month.Text = instance.Model.DisplayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            selectedDate.Text = instance.Model.SelectedDate is { } selected
                ? selected.ToString("D", CultureInfo.CurrentCulture) : "No date selected";
            grid.Children.Clear();
            var first = instance.Model.DisplayedMonth;
            var offset = ((int)first.DayOfWeek - (int)instance.Model.FirstDayOfWeek + 7) % 7;
            for (var day = 1; day <= DateTime.DaysInMonth(first.Year, first.Month); day++)
            {
                var date = new DateOnly(first.Year, first.Month, day);
                var button = new Button
                {
                    Content = instance.Model.SelectedDate == date ? $"{day} selected" : day,
                    MinWidth = 32, MinHeight = 32, Padding = new Thickness(2),
                };
                AutomationProperties.SetName(button,
                    $"{date.ToString("D", CultureInfo.CurrentCulture)}{(instance.Model.SelectedDate == date ? ", selected" : string.Empty)}");
                button.Click += (_, _) => instance.Select(date);
                Grid.SetColumn(button, (offset + day - 1) % 7);
                Grid.SetRow(button, (offset + day - 1) / 7);
                grid.Children.Add(button);
            }
        }
        instance.Updated += OnUpdated;
        panel.Unloaded += (_, _) => instance.Updated -= OnUpdated;
        Refresh();
        return panel;
        void OnUpdated(object? sender, EventArgs args) => panel.DispatcherQueue.TryEnqueue(Refresh);
    }

    public FrameworkElement Timer(TimerWidgetInstance instance)
    {
        var panel = Panel("Timer");
        var remaining = Display("05:00");
        var duration = new NumberBox
        {
            Header = "Minutes",
            Value = ReadNumber(instance.Configuration.Settings, "durationMinutes", 5),
            Minimum = 1, Maximum = 1440,
        };
        ActiveRefreshController? refresh = null;
        void Change(Action action) { action(); refresh?.NotifyStateChanged(); }
        panel.Children.Add(remaining);
        panel.Children.Add(duration);
        panel.Children.Add(Actions(
            TextAction("Start", () => Change(() => instance.Start(TimeSpan.FromMinutes(duration.Value)))),
            TextAction("Pause", () => Change(instance.Pause)),
            TextAction("Resume", () => Change(instance.Resume)),
            TextAction("Reset", () => Change(instance.Reset))));
        refresh = new ActiveRefreshController(panel, TimeSpan.FromMilliseconds(250),
            () => instance.Timer.State.IsRunning && instance.Timer.Remaining > TimeSpan.Zero,
            () => remaining.Text = FormatTime(instance.Timer.Remaining));
        refresh.Attach();
        return panel;
    }

    public FrameworkElement Stopwatch(StopwatchWidgetInstance instance)
    {
        var panel = Panel("Stopwatch");
        var elapsed = Display("00:00.0");
        var laps = Caption("No laps");
        ActiveRefreshController? refresh = null;
        void Change(Action action) { action(); refresh?.NotifyStateChanged(); }
        panel.Children.Add(elapsed);
        panel.Children.Add(Actions(
            TextAction("Start", () => Change(instance.Start)),
            TextAction("Pause", () => Change(instance.Pause)),
            TextAction("Lap", () => Change(instance.Lap)),
            TextAction("Reset", () => Change(instance.Reset))));
        panel.Children.Add(laps);
        refresh = new ActiveRefreshController(panel, TimeSpan.FromMilliseconds(250),
            () => instance.Stopwatch.State.StartedAtUtc is not null,
            () =>
            {
                elapsed.Text = FormatTime(instance.Stopwatch.Elapsed, tenths: true);
                laps.Text = instance.Stopwatch.State.Laps.Count == 0 ? "No laps" :
                    string.Join("  ", instance.Stopwatch.State.Laps.TakeLast(4).Select(lap => FormatTime(lap)));
            });
        refresh.Attach();
        return panel;
    }

    public FrameworkElement Pomodoro(PomodoroWidgetInstance instance)
    {
        var panel = Panel("Pomodoro");
        var phase = Value(instance.Pomodoro.Phase.ToString());
        var remaining = Display("25:00");
        ActiveRefreshController? refresh = null;
        void Change(Action action) { action(); refresh?.NotifyStateChanged(); }
        panel.Children.Add(phase); panel.Children.Add(remaining);
        panel.Children.Add(Actions(
            TextAction("Start", () => Change(instance.Start)),
            TextAction("Pause", () => Change(instance.Pause)),
            TextAction("Reset", () => Change(instance.Reset)),
            TextAction("Next phase", () => Change(instance.Advance))));
        refresh = new ActiveRefreshController(panel, TimeSpan.FromMilliseconds(250),
            () => instance.Pomodoro.Timer.State.IsRunning &&
                instance.Pomodoro.Timer.Remaining > TimeSpan.Zero,
            () =>
            {
                phase.Text = $"{instance.Pomodoro.Phase} · {instance.Pomodoro.CompletedFocusIntervals} completed";
                remaining.Text = FormatTime(instance.Pomodoro.Timer.Remaining);
            });
        refresh.Attach();
        return panel;
    }

    public FrameworkElement Calculator(CalculatorWidgetInstance instance)
    {
        var panel = Panel("Calculator");
        var expression = new TextBox { PlaceholderText = "Expression", MinHeight = 40 };
        var result = Display("0");
        var grid = new Grid { RowSpacing = 4, ColumnSpacing = 4 };
        for (var index = 0; index < 4; index++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        var keys = new[] { "7", "8", "9", "/", "4", "5", "6", "*", "1", "2", "3", "-", "0", ".", "%", "+" };
        for (var index = 0; index < keys.Length; index++)
        {
            if (index % 4 == 0) grid.RowDefinitions.Add(new RowDefinition());
            var key = keys[index];
            var button = new Button { Content = key, MinHeight = 38, HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) => expression.Text += key;
            Grid.SetColumn(button, index % 4); Grid.SetRow(button, index / 4);
            grid.Children.Add(button);
        }
        void Evaluate()
        {
            instance.Clear(); instance.Append(expression.Text); instance.Evaluate();
            result.Text = instance.Result;
        }
        expression.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter) Evaluate();
        };
        panel.Children.Add(expression); panel.Children.Add(result); panel.Children.Add(grid);
        panel.Children.Add(Actions(
            TextAction("Clear", () => { expression.Text = string.Empty; instance.Clear(); }),
            TextAction("Equals", Evaluate)));
        return panel;
    }

    public FrameworkElement Notes(NotesWidgetInstance instance)
    {
        var panel = Panel("Quick Notes");
        var editor = new TextBox
        {
            Text = instance.Model.Text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            MinHeight = 120, PlaceholderText = "Write a note…",
        };
        var status = Caption("Saved locally");
        editor.TextChanged += (_, _) => { instance.Update(editor.Text); status.Text = "Saving locally…"; };
        editor.LostFocus += (_, _) => status.Text = "Saved locally";
        panel.Children.Add(editor); panel.Children.Add(status);
        return panel;
    }
}
