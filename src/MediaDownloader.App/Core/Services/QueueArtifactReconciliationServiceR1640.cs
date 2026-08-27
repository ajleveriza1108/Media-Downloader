using System.Globalization;
using System.IO;
using System.Text;
using MediaDownloader.Core.Models;

namespace MediaDownloader.Core.Services;

public sealed record QueueArtifactProbeR1640(
    string? ExistingPath,
    bool PartialFound,
    string? PartialPath)
{
    public bool Found => !string.IsNullOrWhiteSpace(ExistingPath);
}

public static class QueueArtifactReconciliationServiceR1640
{
    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".webm", ".mov", ".m4v"];
    private static readonly string[] Mp3Extensions = [".mp3"];
    private static readonly string[] M4aExtensions = [".m4a", ".aac"];
    private static readonly string[] FlacExtensions = [".flac"];
    private static readonly string[] PartialSuffixes = [".part", ".ytdl", ".tmp", ".download"];

    public static QueueArtifactProbeR1640 Probe(
        DownloadQueueItem item,
        OutputFormatKind targetKind,
        IEnumerable<string?> roots)
    {
        ArgumentNullException.ThrowIfNull(item);

        var expected = GetExtensions(targetKind);
        var known = item.OutputPath?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(known) &&
            File.Exists(known) &&
            expected.Contains(Path.GetExtension(known), StringComparer.OrdinalIgnoreCase))
        {
            return new QueueArtifactProbeR1640(Path.GetFullPath(known), false, null);
        }

        var titleKeys = new[] { item.Title, item.OriginalTitle }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(NormalizeStem)
            .Where(value => value.Length >= 3)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var sourceId = TryExtractStableSourceId(item.SourceUrl);

        FileInfo? best = null;
        FileInfo? partial = null;

        foreach (var root in NormalizeRoots(roots, known))
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly); }
            catch { continue; }

            foreach (var path in files)
            {
                FileInfo info;
                try { info = new FileInfo(path); }
                catch { continue; }

                var isPartial = PartialSuffixes.Any(suffix =>
                    info.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
                if (isPartial)
                {
                    var partialStem = NormalizeStem(StripPartialSuffixes(info.Name));
                    if (MatchesIdentity(partialStem, info.Name, titleKeys, sourceId) &&
                        (partial is null || info.LastWriteTimeUtc > partial.LastWriteTimeUtc))
                    {
                        partial = info;
                    }
                    continue;
                }

                if (!expected.Contains(info.Extension, StringComparer.OrdinalIgnoreCase)) continue;
                var stem = NormalizeStem(Path.GetFileNameWithoutExtension(info.Name));
                if (!MatchesIdentity(stem, info.Name, titleKeys, sourceId)) continue;
                if (best is null || info.LastWriteTimeUtc > best.LastWriteTimeUtc) best = info;
            }
        }

        return new QueueArtifactProbeR1640(best?.FullName, partial is not null, partial?.FullName);
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        var value = bytes / 1024d;
        if (value < 1024) return value.ToString("0.0", CultureInfo.InvariantCulture) + " KB";
        value /= 1024d;
        if (value < 1024) return value.ToString("0.00", CultureInfo.InvariantCulture) + " MB";
        value /= 1024d;
        return value.ToString("0.00", CultureInfo.InvariantCulture) + " GB";
    }

    public static void RunSelfTestR1640()
    {
        var root = Path.Combine(Path.GetTempPath(), "MediaDock-R1640-Reconcile-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var item = new DownloadQueueItem(
                "Fixture Song", "YouTube", "https://www.youtube.com/watch?v=abc123fixture",
                "1080p", "MP4", OutputFormatKind.Mp4, string.Empty);

            item.Status = "Missing";
            if (!item.CanStart)
                throw new InvalidOperationException("R1.6.40 missing-output queue state must remain downloadable.");
            item.Status = "Partial found";
            if (!item.CanStart)
                throw new InvalidOperationException("R1.6.40 partial-output queue state must remain resumable.");
            item.Status = "Ready";

            var completed = Path.Combine(root, "Fixture Song.mp4");
            File.WriteAllBytes(completed, [1, 2, 3, 4]);
            var found = Probe(item, OutputFormatKind.Mp4, [root]);
            if (!found.Found || !string.Equals(Path.GetFullPath(completed), found.ExistingPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("R1.6.40 artifact reconciliation failed to find an existing completed MP4.");

            File.Delete(completed);
            var partial = Path.Combine(root, "Fixture Song.mp4.part");
            File.WriteAllBytes(partial, [1]);
            var pending = Probe(item, OutputFormatKind.Mp4, [root]);
            if (pending.Found || !pending.PartialFound)
                throw new InvalidOperationException("R1.6.40 artifact reconciliation failed to recognize a partial download.");

            File.Delete(partial);
            var missing = Probe(item, OutputFormatKind.Mp4, [root]);
            if (missing.Found || missing.PartialFound)
                throw new InvalidOperationException("R1.6.40 artifact reconciliation reported a file after it was removed.");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static IReadOnlyList<string> GetExtensions(OutputFormatKind kind) => kind switch
    {
        OutputFormatKind.Mp3 => Mp3Extensions,
        OutputFormatKind.M4a => M4aExtensions,
        OutputFormatKind.Flac => FlacExtensions,
        _ => VideoExtensions
    };

    private static IEnumerable<string> NormalizeRoots(IEnumerable<string?> roots, string knownPath)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in roots.Append(Path.GetDirectoryName(knownPath)))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            string full;
            try { full = Path.GetFullPath(raw); }
            catch { continue; }
            if (Directory.Exists(full) && unique.Add(full)) yield return full;
        }
    }

    private static bool MatchesIdentity(string candidateStem, string candidateName, IReadOnlyList<string> titleKeys, string sourceId)
    {
        if (!string.IsNullOrWhiteSpace(sourceId) && candidateName.Contains(sourceId, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var key in titleKeys)
        {
            if (candidateStem.Equals(key, StringComparison.Ordinal)) return true;
            if (candidateStem.StartsWith(key, StringComparison.Ordinal) && candidateStem.Length - key.Length <= 8) return true;
            if (key.StartsWith(candidateStem, StringComparison.Ordinal) && key.Length - candidateStem.Length <= 4) return true;
        }
        return false;
    }

    private static string NormalizeStem(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Normalize(NormalizationForm.FormKC))
            if (char.IsLetterOrDigit(ch)) builder.Append(char.ToLowerInvariant(ch));
        return builder.ToString();
    }

    private static string StripPartialSuffixes(string name)
    {
        var current = name;
        foreach (var suffix in PartialSuffixes)
        {
            if (current.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                current = current[..^suffix.Length];
                break;
            }
        }
        return Path.GetFileNameWithoutExtension(current);
    }

    private static string TryExtractStableSourceId(string? sourceUrl)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri)) return string.Empty;
        if (uri.Host.Contains("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2 && string.Equals(parts[0], "v", StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(parts[1]);
            }
        }
        if (uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            return uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        if (uri.Host.Contains("tiktok.com", StringComparison.OrdinalIgnoreCase))
            return uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(segment => segment.All(char.IsDigit) && segment.Length >= 8) ?? string.Empty;
        return string.Empty;
    }
}
