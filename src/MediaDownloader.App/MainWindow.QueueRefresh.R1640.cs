using System;
using System.Windows;
using MediaDownloader.Core.Models;

namespace MediaDownloader;

public partial class MainWindow
{
    private async void QueueRefreshAllR1640_Click(object sender, RoutedEventArgs e)
    {
        try { await _viewModel.RefreshAllQueueArtifactsR1640Async(); }
        catch (Exception ex) { _viewModel.ReportStartupWarning("Queue refresh failed. Open Diagnostics for details.", ex); }
    }

    private async void QueueRowRefreshR1640_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not DownloadQueueItem item) return;
        try { await _viewModel.RefreshQueueItemR1640Async(item); }
        catch (Exception ex) { _viewModel.ReportStartupWarning("Queue item refresh failed. Open Diagnostics for details.", ex); }
    }
}
