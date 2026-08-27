using System.IO;
using MediaDownloader.Core.Models;
using MediaDownloader.Core.Services;

namespace MediaDownloader.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool _reconcilingQueueArtifactsR1640;

    public Task RefreshQueueItemR1640Async(DownloadQueueItem item)
    {
        if (Busy)
        {
            Status = "Wait for the current MediaDock operation to finish before refreshing this item.";
            return Task.CompletedTask;
        }

        ReconcileQueueItemArtifactR1640(item, announce: true);
        ReconcileCounterpartsR1640();
        PersistQueueSafely();
        RaiseQueueBulkCanExecuteChanged();
        ClearCompletedCommand.RaiseCanExecuteChanged();
        DownloadCommand.RaiseCanExecuteChanged();
        return Task.CompletedTask;
    }

    public Task RefreshAllQueueArtifactsR1640Async()
    {
        if (Busy)
        {
            Status = "Wait for the current MediaDock operation to finish before refreshing the queue.";
            return Task.CompletedTask;
        }
        ReconcileAllQueueArtifactsR1640(announce: true);
        return Task.CompletedTask;
    }

    private void ReconcileAllQueueArtifactsR1640(bool announce)
    {
        if (_reconcilingQueueArtifactsR1640) return;
        _reconcilingQueueArtifactsR1640 = true;
        try
        {
            var found = 0; var missing = 0; var pending = 0;
            foreach (var item in DownloadQueue)
            {
                var state = ReconcileQueueItemArtifactR1640(item, announce: false);
                if (state == 1) found++; else if (state == 2) missing++; else if (state == 3) pending++;
            }
            ReconcileCounterpartsR1640();
            PersistQueueSafely();
            RaiseQueueBulkCanExecuteChanged();
            ClearCompletedCommand.RaiseCanExecuteChanged();
            DownloadCommand.RaiseCanExecuteChanged();
            if (announce) Status = $"Refresh complete - {found} found, {missing} missing, {pending} in progress.";
        }
        finally { _reconcilingQueueArtifactsR1640 = false; }
    }

    // Returns: 0 unchanged/ready, 1 found, 2 missing, 3 partial/in-progress.
    private int ReconcileQueueItemArtifactR1640(DownloadQueueItem item, bool announce)
    {
        if (item is null) return 0;
        if (IsTransientQueueStatusR1640(item.Status)) return 0;
        var roots = GetArtifactRootsR1640(item);
        var probe = QueueArtifactReconciliationServiceR1640.Probe(item, item.OutputKind, roots);

        if (probe.Found && !string.IsNullOrWhiteSpace(probe.ExistingPath))
        {
            var path = probe.ExistingPath;
            var converted = item.Status.Contains("Convert", StringComparison.OrdinalIgnoreCase) ||
                            item.ProgressText.Contains("Convert", StringComparison.OrdinalIgnoreCase);
            item.OutputPath = path;
            item.OutputFileAvailable = true;
            item.Completed = true;
            item.ProgressPercent = 100;
            item.SpeedText = string.Empty;
            item.Status = converted ? "Converted" : IsAudioOutputKindR1639(item.OutputKind) ? "Audio downloaded" : "Downloaded";
            item.ProgressText = File.Exists(path)
                ? QueueArtifactReconciliationServiceR1640.FormatBytes(new FileInfo(path).Length)
                : "Found on disk";
            if (announce) Status = $"Found: {Path.GetFileName(path)}";
            return 1;
        }

        if (probe.PartialFound)
        {
            item.Completed = false;
            item.OutputFileAvailable = false;
            item.Status = "Partial found";
            item.ProgressText = string.IsNullOrWhiteSpace(probe.PartialPath) ? "Partial output detected" : $"Partial: {Path.GetFileName(probe.PartialPath)}";
            if (announce) Status = "A partial download was found. Download can resume this item.";
            return 3;
        }

        var previouslyExpectedOutput = item.Completed || item.OutputFileAvailable || !string.IsNullOrWhiteSpace(item.OutputPath);
        item.OutputFileAvailable = false;
        if (previouslyExpectedOutput)
        {
            item.Completed = false;
            item.ProgressPercent = 0;
            item.SpeedText = string.Empty;
            item.Status = "Missing";
            item.ProgressText = "Previous output not found - ready to download again";
            if (announce) Status = "The previous output is missing. This item can be downloaded again.";
            return 2;
        }
        if (announce) Status = "No existing output was found. This item is ready to download.";
        return 0;
    }

    private void ReconcileCounterpartsR1640()
    {
        foreach (var item in DownloadQueue)
        {
            var roots = GetArtifactRootsR1640(item);
            item.HasDownloadedMp3Counterpart =
                DownloadQueue.Any(other => !ReferenceEquals(other, item) && other.Completed && other.OutputKind == OutputFormatKind.Mp3 &&
                    string.Equals(other.SourceUrl, item.SourceUrl, StringComparison.OrdinalIgnoreCase) && other.OutputFileAvailable) ||
                QueueArtifactReconciliationServiceR1640.Probe(item, OutputFormatKind.Mp3, roots).Found;
            item.HasDownloadedMp4Counterpart =
                DownloadQueue.Any(other => !ReferenceEquals(other, item) && other.Completed && other.OutputKind == OutputFormatKind.Mp4 &&
                    string.Equals(other.SourceUrl, item.SourceUrl, StringComparison.OrdinalIgnoreCase) && other.OutputFileAvailable) ||
                QueueArtifactReconciliationServiceR1640.Probe(item, OutputFormatKind.Mp4, roots).Found;
        }
    }

    private IEnumerable<string?> GetArtifactRootsR1640(DownloadQueueItem item)
    {
        yield return OutputDirectory;
        yield return ConversionOutputDirectory;
        if (!string.IsNullOrWhiteSpace(item.OutputPath)) yield return Path.GetDirectoryName(item.OutputPath);
    }

    private static bool IsTransientQueueStatusR1640(string? status) =>
        status is not null &&
        (status.Contains("Downloading", StringComparison.OrdinalIgnoreCase) ||
         status.Contains("Analyzing", StringComparison.OrdinalIgnoreCase) ||
         status.Contains("Starting", StringComparison.OrdinalIgnoreCase) ||
         status.Contains("Converting", StringComparison.OrdinalIgnoreCase));
}
