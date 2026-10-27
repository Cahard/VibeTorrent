using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VibeTorrent.App.Behaviors;

/// <summary>
/// Starts the soft highlight sweep used by primary actions. Doing this in code
/// avoids fragile Storyboard target names inside a ControlTemplate, while the
/// animation itself stays on the compositor-friendly TranslateTransform.
/// </summary>
public static class ShimmerBehavior
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled",
        typeof(bool),
        typeof(ShimmerBehavior),
        new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Button button)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            button.Loaded += OnLoaded;
            button.Unloaded += OnUnloaded;
            if (button.IsLoaded)
            {
                Start(button);
            }
        }
        else
        {
            button.Loaded -= OnLoaded;
            button.Unloaded -= OnUnloaded;
            Stop(button);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Dispatcher.BeginInvoke(() => Start(button));
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            Stop(button);
        }
    }

    private static void Start(Button button)
    {
        button.ApplyTemplate();
        if (button.Template.FindName("Shine", button) is not FrameworkElement shine)
        {
            return;
        }

        // Template Freezables are frozen by WPF; assigning a fresh transform keeps
        // the animation local to this button instance.
        var transform = new TranslateTransform(-220, 0);
        shine.RenderTransform = transform;

        var animation = new DoubleAnimation
        {
            From = -220,
            To = 860,
            Duration = TimeSpan.FromSeconds(3.2),
            BeginTime = TimeSpan.FromMilliseconds(650),
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        transform.BeginAnimation(TranslateTransform.XProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private static void Stop(Button button)
    {
        if (button.Template?.FindName("Shine", button) is FrameworkElement shine &&
            shine.RenderTransform is TranslateTransform transform)
        {
            transform.BeginAnimation(TranslateTransform.XProperty, null);
        }
    }
}

