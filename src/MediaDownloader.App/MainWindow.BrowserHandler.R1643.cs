using System.Windows;
using MediaDownloader.Core.Services;

namespace MediaDownloader;

public partial class MainWindow
{
    internal async void AcceptBrowserHandlerRequestR1643(BrowserHandlerRequestR1643 request)
    {
        try
        {
            if (!IsVisible)
            {
                Show();
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            if (TorrentClientR1644.IsTorrentSourceR1644(request.Url))
            {
                await AcceptTorrentBrowserRequestR1644Async(request);
                return;
            }

            if (request.Kind == BrowserHandlerKindR1643.File)
            {
                ShowGeneralDownloaderWorkspaceR1643();
                Activate();

                var choice = DownloadFileDialogR1643.ShowFor(
                    this,
                    request,
                    _viewModel.GeneralDownloadOutputDirectoryR1643);

                if (choice is null)
                {
                    return;
                }

                await _viewModel.QueueGeneralDownloadR1643Async(
                    request,
                    choice.StartImmediately,
                    choice.OutputDirectory,
                    choice.FileName);

                GeneralDownloadUrlTextBoxR1643.Focus();
                return;
            }

            ShowDownloadWorkspace();
            Activate();
            await _viewModel.HandleBrowserRequestR1643Async(request);
            UrlInputTextBox.Focus();
            UrlInputTextBox.CaretIndex = UrlInputTextBox.Text?.Length ?? 0;
        }
        catch (Exception ex)
        {
            App.WriteCrashLog("BrowserHandler.Accept.R1643", ex);
        }
    }
}
