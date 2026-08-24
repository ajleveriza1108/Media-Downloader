using System;
using System.Windows;
using System.Windows.Controls;

namespace MediaDownloader;

public partial class SettingsWindow
{
    private bool _settingsNavSyncR1634;

    private void SettingsNavR1634_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton button || button.Tag is not string sectionKey)
        {
            return;
        }

        var target = ResolveSettingsSectionR1634(sectionKey);
        if (target is null)
        {
            return;
        }

        _settingsNavSyncR1634 = true;
        try
        {
            SettingsContentStack.UpdateLayout();
            SettingsContentScrollViewer.UpdateLayout();

            var point = target.TranslatePoint(new Point(0, 0), SettingsContentStack);
            var targetOffset = Math.Max(0, point.Y - 4);
            SettingsContentScrollViewer.ScrollToVerticalOffset(targetOffset);
            button.IsChecked = true;
        }
        finally
        {
            _settingsNavSyncR1634 = false;
        }
    }

    private void SettingsContentScrollViewerR1634_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_settingsNavSyncR1634 || Math.Abs(e.VerticalChange) < 0.01)
        {
            return;
        }

        UpdateSettingsNavigationFromScrollR1634();
    }

    private FrameworkElement? ResolveSettingsSectionR1634(string sectionKey) =>
        sectionKey switch
        {
            "Audio" => SettingsSectionAudioR1634,
            "Download" => SettingsSectionDownloadR1634,
            "Output" => SettingsSectionOutputR1634,
            "Appearance" => SettingsSectionAppearanceR1634,
            "Window" => SettingsSectionWindowR1634,
            "Updates" => SettingsSectionUpdatesR1634,
            "Support" => SettingsSectionSupportR1634,
            _ => null
        };

    private void UpdateSettingsNavigationFromScrollR1634()
    {
        var sections = new (RadioButton Navigation, FrameworkElement Target)[]
        {
            (SettingsNavAudioR1634, SettingsSectionAudioR1634),
            (SettingsNavDownloadR1634, SettingsSectionDownloadR1634),
            (SettingsNavOutputR1634, SettingsSectionOutputR1634),
            (SettingsNavAppearanceR1634, SettingsSectionAppearanceR1634),
            (SettingsNavWindowR1634, SettingsSectionWindowR1634),
            (SettingsNavUpdatesR1634, SettingsSectionUpdatesR1634),
            (SettingsNavSupportR1634, SettingsSectionSupportR1634)
        };

        if (SettingsContentScrollViewer.ScrollableHeight > 0 &&
            SettingsContentScrollViewer.VerticalOffset >= SettingsContentScrollViewer.ScrollableHeight - 2)
        {
            SettingsNavSupportR1634.IsChecked = true;
            return;
        }

        var currentOffset = SettingsContentScrollViewer.VerticalOffset + 18;
        var selected = sections[0].Navigation;

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

        if (selected.IsChecked != true)
        {
            selected.IsChecked = true;
        }
    }
}
