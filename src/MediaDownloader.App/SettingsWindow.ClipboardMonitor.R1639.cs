using System.Windows;
using System.Windows.Controls;
using MediaDownloader.Core.Services;

namespace MediaDownloader;

public partial class SettingsWindow
{
    private void ClipboardDetectionR1639_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            checkBox.IsChecked = ClipboardMediaLinkPreferencesR1639.IsEnabled();
        }
    }

    private void ClipboardDetectionR1639_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            ClipboardMediaLinkPreferencesR1639.SetEnabled(checkBox.IsChecked == true);
        }
    }
}
