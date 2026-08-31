using System.Windows;
using MediaDownloader.Core.Services;

namespace MediaDownloader;

public partial class MainWindow
{
    private void InstallUniversalDownloaderR1643()
    {
        DownloadViewButton.Click += ExistingWorkspaceNavigationR1643_Click;
        StreamViewButton.Click += ExistingWorkspaceNavigationR1643_Click;
        ConvertViewButton.Click += ExistingWorkspaceNavigationR1643_Click;
    }

    private void ExistingWorkspaceNavigationR1643_Click(object sender, RoutedEventArgs e)
    {
        GeneralDownloaderWorkspace.Visibility = Visibility.Collapsed;
        DownloaderViewButton.BorderBrush = System.Windows.Media.Brushes.Transparent;
    }

    private void ShowGeneralDownloaderR1643_Click(object sender, RoutedEventArgs e) =>
        ShowGeneralDownloaderWorkspaceR1643();

    internal void ShowGeneralDownloaderWorkspaceR1643()
    {
        DownloadWorkspace.Visibility = Visibility.Collapsed;
        StreamWorkspace.Visibility = Visibility.Collapsed;
        ConvertWorkspace.Visibility = Visibility.Collapsed;
        GeneralDownloaderWorkspace.Visibility = Visibility.Visible;

        DownloadViewButton.BorderBrush = System.Windows.Media.Brushes.Transparent;
        StreamViewButton.BorderBrush = System.Windows.Media.Brushes.Transparent;
        ConvertViewButton.BorderBrush = System.Windows.Media.Brushes.Transparent;
        DownloaderViewButton.BorderBrush = WorkspaceAccentBrush;

        GeneralDownloadUrlTextBoxR1643.Focus();
        GeneralDownloadUrlTextBoxR1643.CaretIndex =
            GeneralDownloadUrlTextBoxR1643.Text?.Length ?? 0;
    }

    private async void GeneralDownloadAddR1643_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.QueueGeneralDownloadFromTextR1643Async();
        }
        catch (Exception ex)
        {
            App.WriteCrashLog("GeneralDownloader.Add.R1643", ex);
        }
    }

    private async void GeneralDownloadStartR1643_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: GeneralDownloadItemR1643 item })
        {
            await _viewModel.StartGeneralDownloadR1643Async(item);
        }
    }

    private void GeneralDownloadCancelR1643_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: GeneralDownloadItemR1643 item })
        {
            _viewModel.CancelGeneralDownloadR1643(item);
        }
    }

    private async void GeneralDownloadOpenR1643_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: GeneralDownloadItemR1643 item })
        {
            await _viewModel.OpenGeneralDownloadR1643Async(item);
        }
    }

    private async void GeneralDownloadOpenFolderR1643_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.OpenGeneralDownloadFolderR1643Async();
    }
}
