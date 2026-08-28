using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MediaDownloader.Core.Models;

namespace MediaDownloader;

// MEDIADOCK_FUNCTIONAL_QUEUE_INTERACTIONS_R1641
public partial class MainWindow
{
    private static DownloadQueueItem? QueueItemFromSenderR1641(object sender) =>
        sender is FrameworkElement element
            ? element.DataContext as DownloadQueueItem
            : null;

    private async void QueueRowDownloadR1641_Click(object sender, RoutedEventArgs e) =>
        await RunQueueRowActionR1641Async(
            sender,
            item => _viewModel.DownloadQueueItemAsync(item),
            "Queue download failed.");

    private async void QueueRowSubtitlesR1641_Click(object sender, RoutedEventArgs e) =>
        await RunQueueRowActionR1641Async(
            sender,
            item => _viewModel.DownloadQueueSubtitlesR1641Async(item),
            "Subtitle download failed.");

    private async void QueueRowSourceLinkR1641_Click(object sender, RoutedEventArgs e) =>
        await RunQueueRowActionR1641Async(
            sender,
            item => _viewModel.CopyQueueSourceUrlAsync(item),
            "Copy source link failed.");

    private async void QueueRowConvertMp3R1641_Click(object sender, RoutedEventArgs e) =>
        await RunQueueRowActionR1641Async(
            sender,
            item => _viewModel.ConvertQueueVideoToMp3Async(item),
            "MP3 conversion failed.");

    private async void QueueRowConvertMp4R1641_Click(object sender, RoutedEventArgs e) =>
        await RunQueueRowActionR1641Async(
            sender,
            item => _viewModel.RedownloadQueueMp3AsMp4Async(item),
            "MP4 download failed.");

    private async void QueueRowOpenFileR1641_Click(object sender, RoutedEventArgs e) =>
        await RunQueueRowActionR1641Async(
            sender,
            item => _viewModel.OpenQueueFileAsync(item),
            "Open downloaded file failed.");

    private async void QueueRowRefreshR1641_Click(object sender, RoutedEventArgs e) =>
        await RunQueueRowActionR1641Async(
            sender,
            item => _viewModel.RefreshQueueItemR1640Async(item),
            "Refresh file status failed.");

    private async void QueueRowDeleteR1641_Click(object sender, RoutedEventArgs e)
    {
        var item = QueueItemFromSenderR1641(sender);
        if (item is null)
        {
            return;
        }

        var choice = QueueDeleteDialogR1638.ShowFor(this, $"“{item.Title}”");
        if (choice == QueueDeleteChoiceR1638.Cancel)
        {
            return;
        }

        await _viewModel.RemoveQueueItemsR1641Async(
            [item],
            choice == QueueDeleteChoiceR1638.DeleteFromComputer);
    }

    private async void QueueSelectAllR1641_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox)
        {
            return;
        }

        await _viewModel.SetQueueSelectionR1641Async(checkBox.IsChecked == true);
    }

    private async void QueueWorkspaceDownloadSelectedR1641_Click(object sender, RoutedEventArgs e)
    {
        var selected = _viewModel.DownloadQueue
            .Where(item => item.IsSelected && item.CanStart)
            .ToArray();

        await _viewModel.DownloadQueueBatchR1630Async(selected, "selected items");
    }

    private async void QueueWorkspaceDeleteSelectedR1641_Click(object sender, RoutedEventArgs e)
    {
        var selected = _viewModel.DownloadQueue
            .Where(item => item.IsSelected)
            .ToArray();

        if (selected.Length == 0)
        {
            return;
        }

        var choice = QueueDeleteDialogR1638.ShowFor(
            this,
            $"{selected.Length} selected queue item(s)");
        if (choice == QueueDeleteChoiceR1638.Cancel)
        {
            return;
        }

        await _viewModel.RemoveQueueItemsR1641Async(
            selected,
            choice == QueueDeleteChoiceR1638.DeleteFromComputer);
    }

    private async void QueueWorkspaceDeleteCompletedR1641_Click(object sender, RoutedEventArgs e)
    {
        var completed = _viewModel.DownloadQueue
            .Where(item => item.Completed)
            .ToArray();

        if (completed.Length == 0)
        {
            return;
        }

        var choice = QueueDeleteDialogR1638.ShowFor(
            this,
            $"{completed.Length} completed queue item(s)");
        if (choice == QueueDeleteChoiceR1638.Cancel)
        {
            return;
        }

        await _viewModel.RemoveQueueItemsR1641Async(
            completed,
            choice == QueueDeleteChoiceR1638.DeleteFromComputer);
    }

    private async void QueueWorkspaceDeleteAllR1641_Click(object sender, RoutedEventArgs e)
    {
        var all = _viewModel.DownloadQueue.ToArray();
        if (all.Length == 0)
        {
            return;
        }

        var choice = QueueDeleteDialogR1638.ShowFor(
            this,
            $"all {all.Length} queue item(s)");
        if (choice == QueueDeleteChoiceR1638.Cancel)
        {
            return;
        }

        await _viewModel.RemoveQueueItemsR1641Async(
            all,
            choice == QueueDeleteChoiceR1638.DeleteFromComputer);
    }

    private async void QueueWorkspaceStartR1641_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.StartQueueR1641Async();
        }
        catch (Exception ex)
        {
            _viewModel.ReportFunctionalGuiWarningR1641(
                "Queue start failed. Open Diagnostics for details.",
                ex);
        }
    }

    private async Task RunQueueRowActionR1641Async(
        object sender,
        Func<DownloadQueueItem, Task> action,
        string fallbackMessage)
    {
        var item = QueueItemFromSenderR1641(sender);
        if (item is null)
        {
            return;
        }

        try
        {
            await action(item);
        }
        catch (Exception ex)
        {
            _viewModel.ReportFunctionalGuiWarningR1641(
                fallbackMessage + " Open Diagnostics for details.",
                ex);
        }
    }
}
