using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace MediaDownloader;

public partial class SettingsWindow
{
    private readonly DispatcherTimer _settingsScrollTimerR1635 = new()
    {
        Interval = TimeSpan.FromMilliseconds(15)
    };

    private readonly Stopwatch _settingsScrollClockR1635 = new();
    private bool _settingsNavSyncR1635;
    private bool _settingsNavInitializedR1635;
    private double _settingsScrollStartR1635;
    private double _settingsScrollTargetR1635;

    private void SettingsWindowR1635_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_settingsNavInitializedR1635)
        {
            _settingsScrollTimerR1635.Tick += SettingsScrollTimerR1635_Tick;
            _settingsNavInitializedR1635 = true;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                SettingsContentStack.UpdateLayout();
                SettingsContentScrollViewer.UpdateLayout();
                MoveSettingsIndicatorR1635(SettingsNavAudioR1635, false);
                UpdateSettingsNavigationFromScrollR1635();
            }));
    }

    private void SettingsNavR1635_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton button || button.Tag is not string sectionKey)
        {
            return;
        }

        var target = ResolveSettingsSectionR1635(sectionKey);
        if (target is null)
        {
            return;
        }

        button.IsChecked = true;
        MoveSettingsIndicatorR1635(button, true);
        StartSmoothSettingsScrollR1635(target);
    }

    private void StartSmoothSettingsScrollR1635(FrameworkElement target)
    {
        SettingsContentStack.UpdateLayout();
        SettingsContentScrollViewer.UpdateLayout();

        var point = target.TranslatePoint(new Point(0, 0), SettingsContentStack);
        var desired = Math.Max(0, point.Y - 6);
        var targetOffset = Math.Min(SettingsContentScrollViewer.ScrollableHeight, desired);

        _settingsScrollStartR1635 = SettingsContentScrollViewer.VerticalOffset;
        _settingsScrollTargetR1635 = targetOffset;
        _settingsNavSyncR1635 = true;

        if (Math.Abs(_settingsScrollTargetR1635 - _settingsScrollStartR1635) < 0.5)
        {
            SettingsContentScrollViewer.ScrollToVerticalOffset(_settingsScrollTargetR1635);
            _settingsNavSyncR1635 = false;
            UpdateSettingsNavigationFromScrollR1635();
            return;
        }

        _settingsScrollClockR1635.Restart();
        _settingsScrollTimerR1635.Start();
    }

    private void SettingsScrollTimerR1635_Tick(object? sender, EventArgs e)
    {
        const double durationMs = 240.0;
        var progress = Math.Clamp(_settingsScrollClockR1635.Elapsed.TotalMilliseconds / durationMs, 0.0, 1.0);
        var eased = 1.0 - Math.Pow(1.0 - progress, 3.0);
        var offset = _settingsScrollStartR1635 +
                     ((_settingsScrollTargetR1635 - _settingsScrollStartR1635) * eased);

        SettingsContentScrollViewer.ScrollToVerticalOffset(offset);

        if (progress < 1.0)
        {
            return;
        }

        _settingsScrollTimerR1635.Stop();
        _settingsScrollClockR1635.Stop();
        SettingsContentScrollViewer.ScrollToVerticalOffset(_settingsScrollTargetR1635);
        _settingsNavSyncR1635 = false;
        UpdateSettingsNavigationFromScrollR1635();
    }

    private void SettingsContentScrollViewerR1635_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_settingsNavSyncR1635 || Math.Abs(e.VerticalChange) < 0.01)
        {
            return;
        }

        UpdateSettingsNavigationFromScrollR1635();
    }

    private FrameworkElement? ResolveSettingsSectionR1635(string sectionKey) =>
        sectionKey switch
        {
            "Audio" => SettingsSectionAudioR1635,
            "Download" => SettingsSectionDownloadR1635,
            "Output" => SettingsSectionOutputR1635,
            "Appearance" => SettingsSectionAppearanceR1635,
            "Window" => SettingsSectionWindowR1635,
            "Updates" => SettingsSectionUpdatesR1635,
            "Support" => SettingsSectionSupportR1635,
            _ => null
        };

    private void UpdateSettingsNavigationFromScrollR1635()
    {
        var sections = new (RadioButton Navigation, FrameworkElement Target)[]
        {
            (SettingsNavAudioR1635, SettingsSectionAudioR1635),
            (SettingsNavDownloadR1635, SettingsSectionDownloadR1635),
            (SettingsNavOutputR1635, SettingsSectionOutputR1635),
            (SettingsNavAppearanceR1635, SettingsSectionAppearanceR1635),
            (SettingsNavWindowR1635, SettingsSectionWindowR1635),
            (SettingsNavUpdatesR1635, SettingsSectionUpdatesR1635),
            (SettingsNavSupportR1635, SettingsSectionSupportR1635)
        };

        RadioButton selected;

        if (SettingsContentScrollViewer.ScrollableHeight > 0 &&
            SettingsContentScrollViewer.VerticalOffset >= SettingsContentScrollViewer.ScrollableHeight - 2)
        {
            selected = SettingsNavSupportR1635;
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

        MoveSettingsIndicatorR1635(selected, true);
    }

    private void MoveSettingsIndicatorR1635(RadioButton button, bool animate)
    {
        SettingsNavButtonsHostR1635.UpdateLayout();
        button.UpdateLayout();

        var point = button.TranslatePoint(new Point(0, 0), SettingsNavButtonsHostR1635);
        var targetY = point.Y + Math.Max(0, (button.ActualHeight - SettingsNavIndicatorR1635.ActualHeight) / 2.0);

        if (!animate)
        {
            SettingsNavIndicatorTransformR1635.BeginAnimation(
                System.Windows.Media.TranslateTransform.YProperty,
                null);
            SettingsNavIndicatorTransformR1635.Y = targetY;
            return;
        }

        var animation = new DoubleAnimation
        {
            From = SettingsNavIndicatorTransformR1635.Y,
            To = targetY,
            Duration = TimeSpan.FromMilliseconds(190),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        };

        animation.Completed += (_, _) =>
        {
            SettingsNavIndicatorTransformR1635.BeginAnimation(
                System.Windows.Media.TranslateTransform.YProperty,
                null);
            SettingsNavIndicatorTransformR1635.Y = targetY;
        };

        SettingsNavIndicatorTransformR1635.BeginAnimation(
            System.Windows.Media.TranslateTransform.YProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }
}
