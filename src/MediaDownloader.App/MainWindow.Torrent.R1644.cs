using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        TorrentWorkspaceR1644.AllowDrop = true;
        TorrentWorkspaceR1644.PreviewDragOver += TorrentWorkspaceR1644_PreviewDragOver;
        TorrentWorkspaceR1644.Drop += TorrentWorkspaceR1644_Drop;

        // MEDIADOCK_TORRENT_SYNC_CLOSE_PERSIST_R1646
        // Window.Closing is synchronous and happens before WPF can terminate the process.
        // Persist the torrent list here so an async Closed handler can never lose the session.
        Closing += (_, _) => _viewModel.PersistTorrentSessionOnClosingR1646();

        // MEDIADOCK_TORRENT_RESTORE_AFTER_WINDOW_LOADED_R1653
        Loaded += async (_, _) =>
        {
            try
            {
                await _viewModel.RestorePersistedTorrentsAfterLoadedR1653Async();
            }
            catch (Exception ex)
            {
                App.WriteCrashLog("Torrent.Session.LoadedRestore.R1653", ex);
            }
        };

        Closed += async (_, _) =>
        {
            try
            {
                await _viewModel.DisposeTorrentR1644Async();
            }
            catch (Exception ex)
            {
                App.WriteCrashLog("Torrent.Dispose.R190", ex);
            }
        };
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
        var source = TorrentSourceTextBoxR1644.Text.Trim();
        if (await PrepareAndAddTorrentR190Async(source))
        {
            TorrentSourceTextBoxR1644.Clear();
        }
    }

    // Browser/native-host handoff for magnet links and remote .torrent URLs.
    // The WPF process never parses torrent metadata itself. TorrentHost validates metadata,
    // MediaDock uses the configured default torrent folder, and the torrent starts automatically.
    internal async Task AcceptTorrentBrowserRequestR1644Async(BrowserHandlerRequestR1643 request)
    {
        ShowTorrentWorkspaceR1644();
        Activate();
        TorrentSourceTextBoxR1644.Text = request.Url;
        TorrentSourceTextBoxR1644.CaretIndex = TorrentSourceTextBoxR1644.Text.Length;
        await PrepareAndAddTorrentR190Async(request.Url);
    }

    // Entry point used by the Stream workspace's "Torrent / Magnet" button.
    // It navigates to the full torrent client instead of instantiating MonoTorrent in WPF.
    private void StreamTorrentR1644_Click(object sender, RoutedEventArgs e)
    {
        ShowTorrentWorkspaceR1644();
    }

    private async void TorrentAddFileR1644_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Torrent files (*.torrent)|*.torrent",
            CheckFileExists = true,
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        foreach (var file in dialog.FileNames)
        {
            await PrepareAndAddTorrentR190Async(file);
        }
    }

    // MEDIADOCK_ADD_TORRENT_CONFIRMATION_R1651
    // Opening/dropping a .torrent prepares metadata only. The torrent is not committed
    // until the user confirms save location, file selection and start behavior.
    private async Task<bool> PrepareAndAddTorrentR190Async(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        TorrentPreviewR1644? preview = null;
        var prepared = await TryTorrentUiActionR1644Async(
            "Torrent.IsolatedHost.Prepare.R197",
            async () => preview = await _viewModel.PrepareTorrentR1644Async(source),
            title: "MediaDock Torrent");
        if (!prepared || preview is null)
        {
            return false;
        }

        var addDialog = new TorrentAddDialogR1651(
            this,
            preview,
            _viewModel.TorrentOutputDirectoryR1644,
            _viewModel.TorrentPreferencesR199.AutoStartDownloads);
        if (addDialog.ShowDialog() != true)
        {
            await _viewModel.DiscardPreparedTorrentR1651Async(preview);
            _viewModel.SetTorrentStatusR1651("Torrent add cancelled. No download was created.");
            return false;
        }

        return await TryTorrentUiActionR1644Async(
            "Torrent.IsolatedHost.AddFromDialog.R1651",
            async () =>
            {
                await _viewModel.AddPreparedTorrentR1644Async(
                    preview,
                    addDialog.SavePath,
                    streaming: true,
                    startImmediately: addDialog.StartImmediately,
                    createSubfolder: addDialog.CreateSubfolder);
            },
            title: "MediaDock Torrent");
    }

    private void TorrentWorkspaceR1644_PreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedTorrentFilesR1644(e).Length > 0
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void TorrentWorkspaceR1644_Drop(object sender, DragEventArgs e)
    {
        var files = GetDroppedTorrentFilesR1644(e);
        e.Handled = true;
        foreach (var file in files)
        {
            await PrepareAndAddTorrentR190Async(file);
        }
    }

    private static string[] GetDroppedTorrentFilesR1644(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop) ||
            e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return Array.Empty<string>();
        }

        return files
            .Where(File.Exists)
            .Where(path => Path.GetExtension(path).Equals(".torrent", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private TorrentItemR1644? GetTorrentItemFromSenderR190(object sender)
    {
        return sender is FrameworkElement { Tag: TorrentItemR1644 item }
            ? item
            : _viewModel.SelectedTorrentR1644;
    }

    private async void TorrentStartR1644_Click(object sender, RoutedEventArgs e)
    {
        var item = GetTorrentItemFromSenderR190(sender);
        if (item is not null)
        {
            await TryTorrentUiActionR1644Async("Torrent.Start.R190", () => _viewModel.StartTorrentR1644Async(item), item);
        }
    }

    private async void TorrentPauseR1644_Click(object sender, RoutedEventArgs e)
    {
        var item = GetTorrentItemFromSenderR190(sender);
        if (item is not null)
        {
            await TryTorrentUiActionR1644Async("Torrent.Pause.R190", () => _viewModel.PauseTorrentR1644Async(item), item);
        }
    }

    private async void TorrentStopR1644_Click(object sender, RoutedEventArgs e)
    {
        var item = GetTorrentItemFromSenderR190(sender);
        if (item is not null)
        {
            await TryTorrentUiActionR1644Async("Torrent.Stop.R190", () => _viewModel.StopTorrentR1644Async(item), item);
        }
    }

    private async void TorrentRecheckR1644_Click(object sender, RoutedEventArgs e)
    {
        var item = GetTorrentItemFromSenderR190(sender);
        if (item is not null)
        {
            await TryTorrentUiActionR1644Async("Torrent.Recheck.R190", () => _viewModel.RecheckTorrentR1644Async(item), item);
        }
    }

    private async void TorrentRemoveR190_Click(object sender, RoutedEventArgs e)
    {
        var item = GetTorrentItemFromSenderR190(sender);
        if (item is null) return;
        var deleteData = ConfirmTorrentRemovalR199($"Remove '{item.Name}' from MediaDock?");
        if (deleteData is null) return;
        await TryTorrentUiActionR1644Async("Torrent.Remove.R199", () => _viewModel.RemoveTorrentR1644Async(item, deleteData.Value), item);
    }

    private bool? ConfirmTorrentRemovalR199(string prompt)
    {
        if (!_viewModel.TorrentPreferencesR199.ConfirmRemove) return false;
        var result = ThemedMessageBoxR1646.Show(
            this,
            prompt + "\n\nYES = remove torrent AND downloaded data\nNO = remove torrent but KEEP downloaded data\nCANCEL = do nothing",
            "Remove Torrent",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        return result switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null
        };
    }

    private void TorrentSelectAllR199_Checked(object sender, RoutedEventArgs e) => _viewModel.SetAllTorrentChecksR199(true);
    private void TorrentSelectAllR199_Unchecked(object sender, RoutedEventArgs e) => _viewModel.SetAllTorrentChecksR199(false);

    private async void TorrentRemoveCheckedR199_Click(object sender, RoutedEventArgs e)
    {
        var count = _viewModel.TorrentsR1644.Count(item => item.IsChecked);
        if (count == 0)
        {
            ThemedMessageBoxR1646.Show(this, "Check one or more torrents first.", "MediaDock Torrent", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var deleteData = ConfirmTorrentRemovalR199($"Remove {count} checked torrent(s)?");
        if (deleteData is null) return;
        await TryTorrentUiActionR1644Async("Torrent.RemoveChecked.R199", () => _viewModel.RemoveCheckedTorrentsR199Async(deleteData.Value));
    }

    private async void TorrentRemoveAllR199_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TorrentsR1644.Count == 0) return;
        var deleteData = ConfirmTorrentRemovalR199($"Remove all {_viewModel.TorrentsR1644.Count} loaded torrent(s)?");
        if (deleteData is null) return;
        await TryTorrentUiActionR1644Async("Torrent.RemoveAll.R199", () => _viewModel.RemoveAllTorrentsR199Async(deleteData.Value));
    }

    private async void TorrentMoveUpR199_Click(object sender, RoutedEventArgs e)
    {
        var item = GetTorrentItemFromSenderR190(sender);
        if (item is not null) await _viewModel.MoveTorrentUpR199Async(item);
    }

    private async void TorrentMoveDownR199_Click(object sender, RoutedEventArgs e)
    {
        var item = GetTorrentItemFromSenderR190(sender);
        if (item is not null) await _viewModel.MoveTorrentDownR199Async(item);
    }

    private async void TorrentUpdateTrackersR199_Click(object sender, RoutedEventArgs e)
    {
        var item = GetTorrentItemFromSenderR190(sender);
        if (item is not null)
        {
            await TryTorrentUiActionR1644Async("Torrent.UpdateTrackers.R199", () => _viewModel.UpdateTorrentTrackersR199Async(item), item);
        }
    }

    private async void TorrentSettingsR199_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TorrentPreferencesDialogR199(this, _viewModel.TorrentPreferencesR199);
        if (dialog.ShowDialog() == true)
        {
            await TryTorrentUiActionR1644Async("Torrent.Settings.R199", () => _viewModel.ApplyTorrentPreferencesR199Async(dialog.Settings));
        }
    }

    private async void TorrentFilesR1644_Click(object sender, RoutedEventArgs e)
    {
        var item = GetTorrentItemFromSenderR190(sender);
        if (item is null)
        {
            return;
        }

        await TryTorrentUiActionR1644Async(
            "Torrent.Files.R190",
            async () =>
            {
                var choices = await _viewModel.GetTorrentFilesR1644Async(item);
                if (choices.Count == 0)
                {
                    ThemedMessageBoxR1646.Show(
                        this,
                        "Torrent metadata is not available yet.",
                        "MediaDock Torrent",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var dialog = new TorrentFilesDialogR1644(this, choices);
                if (dialog.ShowDialog() == true)
                {
                    await _viewModel.ApplyTorrentFileChoicesR1644Async(item, dialog.Choices);
                }
            },
            item);
    }

    private async void TorrentDetailsR190_Click(object sender, RoutedEventArgs e)
    {
        var item = GetTorrentItemFromSenderR190(sender);
        if (item is null)
        {
            return;
        }

        await TryTorrentUiActionR1644Async(
            "Torrent.Details.R190",
            async () =>
            {
                var files = await _viewModel.GetTorrentFilesR1644Async(item);
                var trackers = await _viewModel.GetTorrentTrackersR1644Async(item);
                var peers = await _viewModel.GetTorrentPeersR1644Async(item);
                new TorrentDetailsDialogR1644(this, item, files, trackers, peers).ShowDialog();
            },
            item);
    }

    private void TorrentOpenFolderR190_Click(object sender, RoutedEventArgs e)
    {
        var item = GetTorrentItemFromSenderR190(sender);
        var path = item?.SavePath ?? _viewModel.TorrentOutputDirectoryR1644;
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            App.WriteCrashLog("Torrent.OpenFolder.R190", ex);
            ThemedMessageBoxR1646.Show(this, ex.Message, "MediaDock Torrent", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void TorrentStreamR1644_Click(object sender, RoutedEventArgs e)
    {
        var item = GetTorrentItemFromSenderR190(sender);
        if (item is null)
        {
            return;
        }

        await TryTorrentUiActionR1644Async(
            "Torrent.Stream.R190",
            async () =>
            {
                var url = await _viewModel.CreateTorrentStreamUrlR1644Async(item);
                if (string.IsNullOrWhiteSpace(url))
                {
                    throw new InvalidOperationException("No streamable file was found in this torrent.");
                }

                StreamPlayerOverlay.Visibility = Visibility.Collapsed;
                StreamWebView.Source = new Uri(url, UriKind.Absolute);
            },
            item,
            title: "MediaDock Torrent Streaming");
    }

    private async Task<bool> TryTorrentUiActionR1644Async(
        string stage,
        Func<Task> action,
        TorrentItemR1644? item = null,
        string title = "MediaDock Torrent")
    {
        try
        {
            await action();
            return true;
        }
        catch (OperationCanceledException)
        {
            item?.MarkOperationError("Operation canceled");
            return false;
        }
        catch (Exception ex)
        {
            App.WriteCrashLog(stage, ex);
            item?.MarkOperationError(ex.Message);
            ThemedMessageBoxR1646.Show(
                this,
                ex.Message + "\n\nThe torrent engine is isolated from MediaDock, so the main app remains open. A diagnostic was written to the Logs folder.",
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }
}
