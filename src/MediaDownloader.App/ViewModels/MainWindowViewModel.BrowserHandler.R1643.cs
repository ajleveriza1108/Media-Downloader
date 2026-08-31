using MediaDownloader;
using MediaDownloader.Core.Models;
using MediaDownloader.Core.Services;

namespace MediaDownloader.ViewModels;

public sealed partial class MainWindowViewModel
{
    public async Task HandleBrowserRequestR1643Async(BrowserHandlerRequestR1643 request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!BrowserHandlerRequestR1643.TryNormalizeHttpUrl(request.Url, out var normalizedUrl))
        {
            Status = "The browser handler supplied an invalid URL.";
            return;
        }

        request = request with { Url = normalizedUrl };

        if (request.Kind == BrowserHandlerKindR1643.File)
        {
            await QueueGeneralDownloadR1643Async(
                request,
                autoStart: request.Mode == BrowserHandlerModeR1643.Download);
            return;
        }

        if (Busy)
        {
            var outputKind = SelectedDownloadFormat?.Kind ?? OutputFormatKind.Mp4;
            var queued = new DownloadQueueItem(
                string.IsNullOrWhiteSpace(request.Title) ? "Browser media" : request.Title,
                "Browser Grabber",
                normalizedUrl,
                IsAudioOutputKindR1639(outputKind) ? "Best audio" : "Auto - Best available",
                OutputFormatLabelR1639(outputKind),
                outputKind,
                string.Empty,
                SelectedMp3Bitrate?.KilobitsPerSecond ?? 320)
            {
                Status = "Ready",
                ProgressText = "Received from browser"
            };

            DownloadQueue.Insert(0, queued);
            _queuePageIndex = 0;
            RefreshQueuePage();
            Status = "Browser media added to the Media queue while MediaDock is busy.";
            return;
        }

        Url = normalizedUrl;
        _autoAnalyzeCts?.Cancel();
        _autoAnalyzeCts?.Dispose();
        _autoAnalyzeCts = null;

        await AnalyzeUrlAsync(normalizedUrl, CancellationToken.None);

        if (request.Mode != BrowserHandlerModeR1643.Download || HasAnalysisError)
        {
            return;
        }

        if (_playlist is not null)
        {
            Status = "Playlist received from the browser. Review the Media queue, then choose Start Queue.";
            return;
        }

        if (_activeQueueItem?.CanStart == true)
        {
            await DownloadQueueItemAsync(_activeQueueItem);
        }
    }
}
