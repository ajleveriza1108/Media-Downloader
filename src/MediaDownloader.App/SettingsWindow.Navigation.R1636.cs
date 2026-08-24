using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MediaDownloader;

public partial class SettingsWindow
{
    private readonly DispatcherTimer _settingsScrollTimerR1636 = new()
    {
        Interval = TimeSpan.FromMilliseconds(15)
    };

    private readonly Stopwatch _settingsScrollClockR1636 = new();
    private bool _settingsNavSyncR1636;
    private bool _settingsNavInitializedR1636;
    private double _settingsScrollStartR1636;
    private double _settingsScrollTargetR1636;

    private void SettingsWindowR1636_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_settingsNavInitializedR1636)
        {
            _settingsScrollTimerR1636.Tick += SettingsScrollTimerR1636_Tick;
            _settingsNavInitializedR1636 = true;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                SettingsContentStack.UpdateLayout();
                SettingsContentScrollViewer.UpdateLayout();
                MoveSettingsIndicatorR1636(SettingsNavAudioR1636, false);
                UpdateSettingsNavigationFromScrollR1636();
            }));
    }

    private void SettingsNavR1636_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton button || button.Tag is not string sectionKey)
        {
            return;
        }

        var target = ResolveSettingsSectionR1636(sectionKey);
        if (target is null)
        {
            return;
        }

        button.IsChecked = true;
        MoveSettingsIndicatorR1636(button, true);
        StartSmoothSettingsScrollR1636(target);
    }

    private void StartSmoothSettingsScrollR1636(FrameworkElement target)
    {
        SettingsContentStack.UpdateLayout();
        SettingsContentScrollViewer.UpdateLayout();

        var point = target.TranslatePoint(new Point(0, 0), SettingsContentStack);
        var desired = Math.Max(0, point.Y - 6);
        var targetOffset = Math.Min(SettingsContentScrollViewer.ScrollableHeight, desired);

        _settingsScrollStartR1636 = SettingsContentScrollViewer.VerticalOffset;
        _settingsScrollTargetR1636 = targetOffset;
        _settingsNavSyncR1636 = true;

        if (Math.Abs(_settingsScrollTargetR1636 - _settingsScrollStartR1636) < 0.5)
        {
            SettingsContentScrollViewer.ScrollToVerticalOffset(_settingsScrollTargetR1636);
            _settingsNavSyncR1636 = false;
            UpdateSettingsNavigationFromScrollR1636();
            return;
        }

        _settingsScrollClockR1636.Restart();
        _settingsScrollTimerR1636.Start();
    }

    private void SettingsScrollTimerR1636_Tick(object? sender, EventArgs e)
    {
        const double durationMs = 240.0;
        var progress = Math.Clamp(_settingsScrollClockR1636.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
        var eased = 1.0 - Math.Pow(1.0 - progress, 3.0);
        var offset = _settingsScrollStartR1636 +
                     ((_settingsScrollTargetR1636 - _settingsScrollStartR1636) * eased);

        SettingsContentScrollViewer.ScrollToVerticalOffset(offset);

        if (progress < 1.0)
        {
            return;
        }

        _settingsScrollTimerR1636.Stop();
        _settingsScrollClockR1636.Stop();
        SettingsContentScrollViewer.ScrollToVerticalOffset(_settingsScrollTargetR1636);
        _settingsNavSyncR1636 = false;
        UpdateSettingsNavigationFromScrollR1636();
    }

    private void SettingsContentScrollViewerR1636_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_settingsNavSyncR1636 || Math.Abs(e.VerticalChange) < 0.01)
        {
            return;
        }

        UpdateSettingsNavigationFromScrollR1636();
    }

    private FrameworkElement? ResolveSettingsSectionR1636(string sectionKey) =>
        sectionKey switch
        {
            "Audio" => SettingsSectionAudioR1636,
            "Download" => SettingsSectionDownloadR1636,
            "Output" => SettingsSectionOutputR1636,
            "Appearance" => SettingsSectionAppearanceR1636,
            "Window" => SettingsSectionWindowR1636,
            "Updates" => SettingsSectionUpdatesR1636,
            "Support" => SettingsSectionSupportR1636,
            _ => null
        };

    private void UpdateSettingsNavigationFromScrollR1636()
    {
        var sections = new (RadioButton Navigation, FrameworkElement Target)[]
        {
            (SettingsNavAudioR1636, SettingsSectionAudioR1636),
            (SettingsNavDownloadR1636, SettingsSectionDownloadR1636),
            (SettingsNavOutputR1636, SettingsSectionOutputR1636),
            (SettingsNavAppearanceR1636, SettingsSectionAppearanceR1636),
            (SettingsNavWindowR1636, SettingsSectionWindowR1636),
            (SettingsNavUpdatesR1636, SettingsSectionUpdatesR1636),
            (SettingsNavSupportR1636, SettingsSectionSupportR1636)
        };

        RadioButton selected;

        if (SettingsContentScrollViewer.ScrollableHeight > 0 &&
            SettingsContentScrollViewer.VerticalOffset >= SettingsContentScrollViewer.ScrollableHeight - 2)
        {
            selected = SettingsNavSupportR1636;
        }
        else
        {
            var currentOffset = SettingsContentScrollViewer.VerticalOffset + 22;
            selected = sections[0].Navigation;

            foreach (var section in sections)
            {
                var point = section.Target.TranslatePoint(new Point(0, 0), SettingsContentStack);
                if (point.Y <= currentOffset)
                {
                    selected = section.Navigation;
                    continue;
                }

                break;
            }
        }

        if (selected.IsChecked != true)
        {
            selected.IsChecked = true;
        }

        MoveSettingsIndicatorR1636(selected, true);
    }

    private void MoveSettingsIndicatorR1636(RadioButton button, bool animate)
    {
        SettingsNavButtonsHostR1636.UpdateLayout();
        button.UpdateLayout();

        var point = button.TranslatePoint(new Point(0, 0), SettingsNavButtonsHostR1636);
        var targetY = point.Y + Math.Max(0, (button.ActualHeight - SettingsNavIndicatorR1636.ActualHeight) / 2.0);

        if (!animate)
        {
            SettingsNavIndicatorTransformR1636.BeginAnimation(
                System.Windows.Media.TranslateTransform.YProperty,
                null);
            SettingsNavIndicatorTransformR1636.Y = targetY;
            return;
        }

        var animation = new DoubleAnimation
        {
            From = SettingsNavIndicatorTransformR1636.Y,
            To = targetY,
            Duration = TimeSpan.FromMilliseconds(190),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };

        animation.Completed += (_, _) =>
        {
            SettingsNavIndicatorTransformR1636.BeginAnimation(
                System.Windows.Media.TranslateTransform.YProperty,
                null);
            SettingsNavIndicatorTransformR1636.Y = targetY;
        };

        SettingsNavIndicatorTransformR1636.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }
}
