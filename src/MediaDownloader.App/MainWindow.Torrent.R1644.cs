using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using MediaDownloader.Core.Services;
using Microsoft.Win32;

namespace MediaDownloader;

public partial class MainWindow
{
    private void InstallTorrentWorkspaceR1644()
    {
        DownloadViewButton.Click += ExistingWorkspaceNavigationR1644_Click;
        DownloaderViewButton.Click += ExistingWorkspaceNavigationR1644_Click;
        StreamViewButton.Click += ExistingWorkspaceNavigationR1644_Click;
        ConvertViewButton.Click += ExistingWorkspaceNavigationR1644_Click;
        Closed += async (_, _) => await _viewModel.DisposeTorrentR1644Async();
    }

    private void ExistingWorkspaceNavigationR1644_Click(object sender, RoutedEventArgs e)
    {
        TorrentWorkspaceR1644.Visibility = Visibility.Collapsed;
        TorrentViewButtonR1644.BorderBrush = System.Windows.Media.Brushes.Transparent;
    }

    private void ShowTorrentR1644_Click(object sender, RoutedEventArgs e) => ShowTorrentWorkspaceR1644();

    internal void ShowTorrentWorkspaceR1644()
    {
        DownloadWorkspace.Visibility = Visibility.Collapsed;
        GeneralDownloaderWorkspace.Visibility = Visibility.Collapsed;
        StreamWorkspace.Visibility = Visibility.Collapsed;
        ConvertWorkspace.Visibility = Visibility.Collapsed;
        TorrentWorkspaceR1644.Visibility = Visibility.Visible;
        DownloadViewButton.BorderBrush = System.Windows.Media.Brushes.Transparent;
        DownloaderViewButton.BorderBrush = System.Windows.Media.Brushes.Transparent;
        StreamViewButton.BorderBrush = System.Windows.Media.Brushes.Transparent;
        ConvertViewButton.BorderBrush = System.Windows.Media.Brushes.Transparent;
        TorrentViewButtonR1644.BorderBrush = WorkspaceAccentBrush;
        TorrentSourceTextBoxR1644.Focus();
    }

    private async void TorrentAddMagnetR1644_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.AddTorrentR1644Async(TorrentSourceTextBoxR1644.Text.Trim(), streaming: false);
            TorrentSourceTextBoxR1644.Clear();
        }
        catch (Exception ex) { App.WriteCrashLog("Torrent.Add.R1644", ex); MessageBox.Show(this, ex.Message, "MediaDock Torrent", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void TorrentAddFileR1644_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Torrent files (*.torrent)|*.torrent", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        try { await _viewModel.AddTorrentR1644Async(dialog.FileName, streaming: false); }
        catch (Exception ex) { App.WriteCrashLog("Torrent.File.R1644", ex); MessageBox.Show(this, ex.Message, "MediaDock Torrent", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void TorrentStartR1644_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TorrentItemR1644 item }) await _viewModel.StartTorrentR1644Async(item);
    }
    private async void TorrentPauseR1644_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TorrentItemR1644 item }) await _viewModel.PauseTorrentR1644Async(item);
    }
    private async void TorrentStopR1644_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TorrentItemR1644 item }) await _viewModel.StopTorrentR1644Async(item);
    }
    private async void TorrentRecheckR1644_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TorrentItemR1644 item }) await _viewModel.RecheckTorrentR1644Async(item);
    }
    private async void TorrentFilesR1644_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TorrentItemR1644 item }) return;
        var choices = _viewModel.GetTorrentFilesR1644(item);
        if (choices.Count == 0) { MessageBox.Show(this, "Torrent metadata is not available yet.", "MediaDock Torrent"); return; }
        var dialog = new TorrentFilesDialogR1644(this, choices);
        if (dialog.ShowDialog() == true) await _viewModel.ApplyTorrentFileChoicesR1644Async(item, dialog.Choices);
    }
    private async void TorrentStreamR1644_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TorrentItemR1644 item }) return;
        try
        {
            var url = await _viewModel.CreateTorrentStreamUrlR1644Async(item);
            if (string.IsNullOrWhiteSpace(url)) { MessageBox.Show(this, "No streamable file is available in this torrent yet.", "MediaDock Torrent"); return; }
            TorrentWorkspaceR1644.Visibility = Visibility.Collapsed;
            StreamWorkspace.Visibility = Visibility.Visible;
            TorrentViewButtonR1644.BorderBrush = System.Windows.Media.Brushes.Transparent;
            StreamViewButton.BorderBrush = WorkspaceAccentBrush;
            StreamPlayerOverlay.Visibility = Visibility.Collapsed;
            StreamWebView.Source = new Uri(url, UriKind.Absolute);
        }
        catch (Exception ex) { App.WriteCrashLog("Torrent.Stream.R1644", ex); MessageBox.Show(this, ex.Message, "MediaDock Torrent", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    internal async Task AcceptTorrentBrowserRequestR1644Async(BrowserHandlerRequestR1643 request)
    {
        ShowTorrentWorkspaceR1644();
        Activate();
        var item = await _viewModel.AddTorrentR1644Async(request.Url, streaming: false);
        item.Refresh();
    }

    private async void StreamTorrentR1644_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var source = _viewModel.StreamUrl?.Trim() ?? string.Empty;
            if (!TorrentClientR1644.IsTorrentSourceR1644(source))
            {
                var dialog = new OpenFileDialog { Filter = "Torrent files (*.torrent)|*.torrent", CheckFileExists = true, Multiselect = false };
                if (dialog.ShowDialog(this) != true) return;
                source = dialog.FileName;
            }
            var item = await _viewModel.AddTorrentR1644Async(source, streaming: true);
            var url = await _viewModel.CreateTorrentStreamUrlR1644Async(item);
            if (string.IsNullOrWhiteSpace(url)) throw new InvalidOperationException("No streamable file was found in this torrent.");
            StreamPlayerOverlay.Visibility = Visibility.Collapsed;
            StreamWebView.Source = new Uri(url, UriKind.Absolute);
        }
        catch (Exception ex) { App.WriteCrashLog("Stream.Torrent.R1644", ex); MessageBox.Show(this, ex.Message, "MediaDock Torrent Streaming", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
