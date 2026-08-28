using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MediaDownloader.Core.Models;

namespace MediaDownloader.ViewModels;

// MEDIADOCK_FUNCTIONAL_GUI_VIEWMODEL_R1641
public sealed partial class MainWindowViewModel
{
    public void ReportFunctionalGuiWarningR1641(
        string message,
        Exception exception)
    {
        Diagnostics = exception.ToString();
        Status = message;
        App.WriteCrashLog("FunctionalGuiR1641", exception);
    }

    public string PremiumStreamNavigationHintR1641 =>
        PremiumWorkspacesEnabled
            ? "Open Stream"
            : "Stream requires an activated MediaDock license.";

    public string PremiumConvertNavigationHintR1641 =>
        PremiumWorkspacesEnabled
            ? "Open Convert"
            : "Convert requires an activated MediaDock license.";

    public async Task DownloadQueueSubtitlesR1641Async(DownloadQueueItem item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.SourceUrl))
        {
            Status = "This queue item has no usable source link for subtitles.";
            return;
        }

        if (Busy)
        {
            Status = "Wait for the current MediaDock operation to finish.";
            return;
        }

        Busy = true;
        try
        {
            Status = $"Checking subtitles for {item.Title}...";
            var analysis = await _mediaEngine.AnalyzeAsync(item.SourceUrl);
            if (analysis.Media is null || analysis.Playlist is not null)
            {
                Status = "This queued source could not be resolved as one media item for subtitles.";
                return;
            }

            var files = (await _mediaEngine.DownloadSubtitlesAsync(
                    analysis.Media,
                    OutputDirectory))
                .ToArray();

            LastDownloadedFile = files.FirstOrDefault() ?? string.Empty;
            Status = files.Length switch
            {
                0 => "No downloadable subtitles were returned for this item.",
                1 => $"Downloaded subtitle: {Path.GetFileName(files[0])}",
                _ => $"Downloaded {files.Length} subtitle files."
            };
        }
        catch (Exception ex)
        {
            Diagnostics = ex.ToString();
            Status = "Subtitle download failed. Open Diagnostics for details.";
            App.WriteCrashLog("QueueSubtitleR1641", ex);
        }
        finally
        {
            Busy = false;
        }
    }

    public Task SetQueueSelectionR1641Async(bool selected)
    {
        foreach (var item in DownloadQueue)
        {
            item.IsSelected = selected;
        }

        NotifyQueueSelectionChanged();
        return Task.CompletedTask;
    }

    public async Task StartQueueR1641Async()
    {
        var selectedReady = DownloadQueue
            .Where(item => item.IsSelected && item.CanStart)
            .ToArray();

        var jobs = selectedReady.Length > 0
            ? selectedReady
            : DownloadQueue.Where(item => item.CanStart).ToArray();

        await DownloadQueueBatchR1630Async(
            jobs,
            selectedReady.Length > 0 ? "selected items" : "ready items");
    }

    public Task RemoveQueueItemsR1641Async(
        IEnumerable<DownloadQueueItem> source,
        bool deleteOutputFiles)
    {
        var items = source
            .Where(item => item is not null)
            .Distinct()
            .ToArray();

        if (items.Length == 0)
        {
            Status = "No queue items matched this action.";
            return Task.CompletedTask;
        }

        var removed = 0;
        var deleteFailures = new List<string>();

        foreach (var item in items)
        {
            if (deleteOutputFiles &&
                !string.IsNullOrWhiteSpace(item.OutputPath) &&
                File.Exists(item.OutputPath))
            {
                try
                {
                    File.Delete(item.OutputPath);
                }
                catch (Exception ex)
                {
                    deleteFailures.Add($"{Path.GetFileName(item.OutputPath)}: {ex.Message}");
                    continue;
                }
            }

            if (DownloadQueue.Remove(item))
            {
                removed++;
            }
        }

        _activeQueueItem = DownloadQueue.FirstOrDefault();
        RefreshCounterpartAvailability();
        RefreshQueuePage();
        PersistQueueSafely();
        NotifyQueueSelectionChanged();
        RaiseQueueBulkCanExecuteChanged();
        ClearCompletedCommand.RaiseCanExecuteChanged();
        DownloadCommand.RaiseCanExecuteChanged();

        if (deleteFailures.Count > 0)
        {
            Diagnostics = string.Join(Environment.NewLine, deleteFailures);
            Status = $"Removed {removed} queue item(s). {deleteFailures.Count} file deletion(s) failed; open Diagnostics for details.";
        }
        else
        {
            Status = deleteOutputFiles
                ? $"Removed {removed} queue item(s) and deleted available output file(s)."
                : $"Removed {removed} queue item(s) from MediaDock.";
        }

        return Task.CompletedTask;
    }
}
