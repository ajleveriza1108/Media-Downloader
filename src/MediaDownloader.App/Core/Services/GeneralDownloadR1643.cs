using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaDownloader;

namespace MediaDownloader.Core.Services;

public enum GeneralDownloadStateR1643
{
    Ready,
    Queued,
    Downloading,
    Paused,
    Completed,
    Failed
}

public sealed class GeneralDownloadItemR1643 : INotifyPropertyChanged
{
    private string _fileName = string.Empty;
    private string _outputPath = string.Empty;
    private string _outputDirectory = string.Empty;
    private long _bytesReceived;
    private long _totalBytes;
    private double _progressPercent;
    private string _statusText = "Ready";
    private string _speedText = string.Empty;
    private GeneralDownloadStateR1643 _state = GeneralDownloadStateR1643.Ready;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Url { get; init; } = string.Empty;
    public string Referrer { get; init; } = string.Empty;
    public string MimeType { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;

    public string FileName
    {
        get => _fileName;
        set => Set(ref _fileName, value);
    }

    public string OutputDirectory
    {
        get => _outputDirectory;
        set => Set(ref _outputDirectory, value);
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (Set(ref _outputPath, value))
            {
                OnPropertyChanged(nameof(CanOpenR1643));
            }
        }
    }

    public long BytesReceived
    {
        get => _bytesReceived;
        set
        {
            if (Set(ref _bytesReceived, value))
            {
                OnPropertyChanged(nameof(SizeTextR1643));
            }
        }
    }

    public long TotalBytes
    {
        get => _totalBytes;
        set
        {
            if (Set(ref _totalBytes, value))
            {
                OnPropertyChanged(nameof(SizeTextR1643));
            }
        }
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        set => Set(ref _progressPercent, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    public string SpeedText
    {
        get => _speedText;
        set => Set(ref _speedText, value);
    }

    public GeneralDownloadStateR1643 State
    {
        get => _state;
        set
        {
            if (Set(ref _state, value))
            {
                OnPropertyChanged(nameof(CanStartR1643));
                OnPropertyChanged(nameof(CanCancelR1643));
                OnPropertyChanged(nameof(CanOpenR1643));
            }
        }
    }

    public bool CanStartR1643 =>
        State is GeneralDownloadStateR1643.Ready
            or GeneralDownloadStateR1643.Paused
            or GeneralDownloadStateR1643.Failed;

    public bool CanCancelR1643 =>
        State is GeneralDownloadStateR1643.Queued
            or GeneralDownloadStateR1643.Downloading;

    public bool CanOpenR1643 =>
        State == GeneralDownloadStateR1643.Completed &&
        !string.IsNullOrWhiteSpace(OutputPath) &&
        File.Exists(OutputPath);

    public string SizeTextR1643
    {
        get
        {
            if (TotalBytes > 0)
            {
                return $"{FormatBytes(BytesReceived)} / {FormatBytes(TotalBytes)}";
            }

            return BytesReceived > 0 ? FormatBytes(BytesReceived) : string.Empty;
        }
    }

    internal CancellationTokenSource? ActiveCancellationR1643 { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string FormatBytes(long value)
    {
        if (value < 1024)
        {
            return $"{value} B";
        }

        var size = (double)value;
        string[] units = ["KB", "MB", "GB", "TB"];
        foreach (var unit in units)
        {
            size /= 1024d;
            if (size < 1024d || unit == "TB")
            {
                return size >= 100d ? $"{size:0} {unit}" : $"{size:0.0} {unit}";
            }
        }

        return $"{value} B";
    }
}

public static class GeneralDownloadClassifierR1643
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "3g2","3gp","7z","aac","ac3","ace","aif","aiff","amr","ape","apk","appx","appxbundle","arj","asf","avi",
        "azw","azw3","bin","bmp","br","bz2","cab","cbr","cbz","ckpt","csv","deb","djvu","dmg","doc","docm","docx",
        "epub","exe","fb2","flac","flv","ggml","gguf","gif","gz","gzip","heic","ico","img","iso","jar","jpeg","jpg",
        "json","lz","lz4","lzh","lzma","m2ts","m2v","m4a","m4b","m4v","mid","midi","mka","mkv","mobi","mov","mp3",
        "mp4","mpa","mpd","mpe","mpeg","mpg","msi","msix","msixbundle","msu","mts","odp","ods","odt","oga","ogg","ogv",
        "onnx","opus","ova","ovf","pdf","pkg","plj","png","pps","ppsx","ppt","pptm","pptx","psd","pt","pth","qcow","qcow2",
        "qt","ra","rar","rm","rmvb","rpm","rtf","safetensors","sea","sit","sitx","snap","sql","svg","tar","tbz","tbz2",
        "tgz","tif","tiff","torrent","ts","ttf","txt","txz","vdi","vhd","vhdx","vmdk","vob","wav","webm","webp","wim",
        "wma","wmv","woff","woff2","xls","xlsb","xlsm","xlsx","xml","xpi","xz","z","zip","zipx","zst","ass","srt","vtt"
    };

    public static bool IsSupportedExtensionR1643(string? extension)
    {
        var normalized = (extension ?? string.Empty).Trim().TrimStart('.');
        return SupportedExtensions.Contains(normalized) ||
               (normalized.Length is >= 3 and <= 4 &&
                (normalized[0] is 'r' or 'R') &&
                normalized[1..].All(char.IsDigit));
    }

    public static string ResolveSuggestedFileNameR1643(BrowserHandlerRequestR1643 request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var supplied = SanitizeFileNameR1643(request.FileName);
        if (!string.IsNullOrWhiteSpace(supplied))
        {
            return supplied;
        }

        if (Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
        {
            var candidate = Uri.UnescapeDataString(Path.GetFileName(uri.LocalPath));
            candidate = SanitizeFileNameR1643(candidate);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return "download.bin";
    }

    public static string SanitizeFileNameR1643(string? value)
    {
        var fileName = Path.GetFileName((value ?? string.Empty).Trim());
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '_');
        }

        fileName = fileName.Trim().TrimEnd('.');
        return fileName.Length > 220 ? fileName[..220] : fileName;
    }

    public static void RunSelfTestR1643()
    {
        foreach (var extension in new[] { ".mp4", ".7z", ".apk", ".epub", ".gguf", ".r00", ".safetensors" })
        {
            if (!IsSupportedExtensionR1643(extension))
            {
                throw new InvalidOperationException($"R1.6.43 universal file-type contract failed for {extension}.");
            }
        }

        var request = new BrowserHandlerRequestR1643(
            2,
            BrowserHandlerModeR1643.Download,
            BrowserHandlerKindR1643.File,
            "https://example.com/releases/archive.zip?token=1",
            "Archive",
            string.Empty,
            "application/zip",
            string.Empty,
            0,
            "self-test");

        if (!string.Equals(ResolveSuggestedFileNameR1643(request), "archive.zip", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("R1.6.43 universal filename-resolution contract failed.");
        }
    }
}

public sealed class GeneralDownloadServiceR1643
{
    private readonly HttpClient _client;

    public GeneralDownloadServiceR1643()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            MaxConnectionsPerServer = 8
        };

        _client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async Task DownloadAsync(
        GeneralDownloadItemR1643 item,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);

        Directory.CreateDirectory(outputDirectory);

        if (string.IsNullOrWhiteSpace(item.OutputPath) ||
            !IsPathInsideDirectory(item.OutputPath, outputDirectory))
        {
            item.OutputPath = GetUniqueOutputPath(
                outputDirectory,
                GeneralDownloadClassifierR1643.SanitizeFileNameR1643(item.FileName) is { Length: > 0 } name
                    ? name
                    : "download.bin");
        }

        var finalPath = item.OutputPath;
        var partialPath = finalPath + ".mediadock.part";
        var existingBytes = File.Exists(partialPath) ? new FileInfo(partialPath).Length : 0L;

        item.State = GeneralDownloadStateR1643.Downloading;
        item.StatusText = existingBytes > 0 ? "Resuming" : "Downloading";
        item.BytesReceived = existingBytes;
        item.SpeedText = string.Empty;

        using var response = await SendAsync(item, existingBytes, cancellationToken);

        if (existingBytes > 0 && response.StatusCode != HttpStatusCode.PartialContent)
        {
            response.Dispose();
            File.Delete(partialPath);
            existingBytes = 0;
            item.BytesReceived = 0;

            using var restartResponse = await SendAsync(item, 0, cancellationToken);
            await CopyResponseAsync(item, restartResponse, partialPath, finalPath, 0, cancellationToken);
            return;
        }

        await CopyResponseAsync(item, response, partialPath, finalPath, existingBytes, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        GeneralDownloadItemR1643 item,
        long existingBytes,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
        request.Headers.UserAgent.ParseAdd("MediaDock/1.6.54");
        request.Headers.Accept.ParseAdd("*/*");

        if (existingBytes > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingBytes, null);
        }

        if (Uri.TryCreate(item.Referrer, UriKind.Absolute, out var referrer) &&
            (referrer.Scheme == Uri.UriSchemeHttp || referrer.Scheme == Uri.UriSchemeHttps))
        {
            request.Headers.Referrer = referrer;
        }

        var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && existingBytes > 0)
        {
            return response;
        }

        response.EnsureSuccessStatusCode();
        return response;
    }

    private static async Task CopyResponseAsync(
        GeneralDownloadItemR1643 item,
        HttpResponseMessage response,
        string partialPath,
        string finalPath,
        long existingBytes,
        CancellationToken cancellationToken)
    {
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentRange?.Length;
        if (total is null && response.Content.Headers.ContentLength is long contentLength)
        {
            total = existingBytes + contentLength;
        }

        if (total is > 0)
        {
            item.TotalBytes = total.Value;
        }

        var mode = existingBytes > 0 ? FileMode.Append : FileMode.Create;
        await using var file = new FileStream(
            partialPath,
            mode,
            FileAccess.Write,
            FileShare.Read,
            256 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);

        var buffer = new byte[256 * 1024];
        var received = existingBytes;
        var lastReported = Stopwatch.GetTimestamp();
        var lastBytes = received;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;

            var now = Stopwatch.GetTimestamp();
            if (Stopwatch.GetElapsedTime(lastReported, now) >= TimeSpan.FromMilliseconds(200))
            {
                var elapsed = Stopwatch.GetElapsedTime(lastReported, now).TotalSeconds;
                var bytesPerSecond = elapsed > 0 ? (received - lastBytes) / elapsed : 0d;

                item.BytesReceived = received;
                item.ProgressPercent = item.TotalBytes > 0
                    ? Math.Min(100d, received * 100d / item.TotalBytes)
                    : 0d;
                item.SpeedText = bytesPerSecond > 0
                    ? $"{FormatRate(bytesPerSecond)}/s"
                    : string.Empty;

                lastReported = now;
                lastBytes = received;
            }
        }

        await file.FlushAsync(cancellationToken);

        item.BytesReceived = received;
        item.ProgressPercent = 100d;
        item.SpeedText = string.Empty;

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }

        File.Move(partialPath, finalPath);
        item.State = GeneralDownloadStateR1643.Completed;
        item.StatusText = "Completed";
    }

    private static bool IsPathInsideDirectory(string candidatePath, string directory)
    {
        try
        {
            var root = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(candidatePath);
            return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string GetUniqueOutputPath(string directory, string fileName)
    {
        var basePath = Path.Combine(directory, fileName);
        if (!File.Exists(basePath) && !File.Exists(basePath + ".mediadock.part"))
        {
            return basePath;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var index = 2; index < 10000; index++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({index}){extension}");
            if (!File.Exists(candidate) && !File.Exists(candidate + ".mediadock.part"))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{stem}-{Guid.NewGuid():N}{extension}");
    }

    private static string FormatRate(double bytesPerSecond)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = bytesPerSecond;
        foreach (var unit in units)
        {
            if (value < 1024d || unit == "GB")
            {
                return value >= 100d ? $"{value:0} {unit}" : $"{value:0.0} {unit}";
            }

            value /= 1024d;
        }

        return $"{bytesPerSecond:0} B";
    }
}

internal sealed record GeneralDownloadSnapshotR1643(
    string Id,
    string Url,
    string FileName,
    string OutputPath,
    string Referrer,
    string MimeType,
    string Source,
    long BytesReceived,
    long TotalBytes,
    double ProgressPercent,
    string StatusText,
    GeneralDownloadStateR1643 State);

public sealed class GeneralDownloadStoreR1643
{
    private readonly string _path;

    public GeneralDownloadStoreR1643()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AJCoder",
            "MediaDock");

        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "general-downloads-r1643.json");
    }

    public IReadOnlyList<GeneralDownloadItemR1643> Load()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            var snapshots = JsonSerializer.Deserialize<List<GeneralDownloadSnapshotR1643>>(
                File.ReadAllBytes(_path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            return snapshots.Take(500).Select(snapshot =>
            {
                var state = snapshot.State;
                var status = snapshot.StatusText;

                if (state is GeneralDownloadStateR1643.Downloading or GeneralDownloadStateR1643.Queued)
                {
                    state = File.Exists(snapshot.OutputPath + ".mediadock.part")
                        ? GeneralDownloadStateR1643.Paused
                        : GeneralDownloadStateR1643.Ready;
                    status = state == GeneralDownloadStateR1643.Paused
                        ? "Paused - resume available"
                        : "Ready";
                }

                if (state == GeneralDownloadStateR1643.Completed && !File.Exists(snapshot.OutputPath))
                {
                    state = GeneralDownloadStateR1643.Failed;
                    status = "Downloaded file is missing";
                }

                return new GeneralDownloadItemR1643
                {
                    Id = snapshot.Id,
                    Url = snapshot.Url,
                    FileName = snapshot.FileName,
                    OutputPath = snapshot.OutputPath,
                    Referrer = snapshot.Referrer,
                    MimeType = snapshot.MimeType,
                    Source = snapshot.Source,
                    BytesReceived = snapshot.BytesReceived,
                    TotalBytes = snapshot.TotalBytes,
                    ProgressPercent = snapshot.ProgressPercent,
                    StatusText = status,
                    State = state
                };
            }).ToArray();
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<GeneralDownloadItemR1643> items)
    {
        var snapshots = items.Take(500).Select(item => new GeneralDownloadSnapshotR1643(
            item.Id,
            item.Url,
            item.FileName,
            item.OutputPath,
            item.Referrer,
            item.MimeType,
            item.Source,
            item.BytesReceived,
            item.TotalBytes,
            item.ProgressPercent,
            item.StatusText,
            item.State)).ToArray();

        var temp = _path + ".tmp";
        File.WriteAllBytes(
            temp,
            JsonSerializer.SerializeToUtf8Bytes(
                snapshots,
                new JsonSerializerOptions { WriteIndented = true }));

        File.Move(temp, _path, true);
    }
}
