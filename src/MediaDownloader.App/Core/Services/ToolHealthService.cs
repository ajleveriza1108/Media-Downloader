using System.Text.RegularExpressions;
using MediaDownloader.Core.Models;

namespace MediaDownloader.Core.Services;

public sealed class ToolHealthService
{
    private readonly ToolLocator _tools;
    private readonly ProcessRunner _runner;

    public ToolHealthService(ToolLocator tools, ProcessRunner runner)
    {
        _tools = tools;
        _runner = runner;
    }

    public async Task<IReadOnlyList<ToolHealth>> CheckAsync(CancellationToken cancellationToken = default)
    {
        var checks = new[]
        {
            CheckOneAsync("yt-dlp", "yt-dlp.exe", ["--version"], cancellationToken),
            CheckOneAsync("Deno", "deno.exe", ["--version"], cancellationToken),
            CheckOneAsync("FFmpeg", "ffmpeg.exe", ["-version"], cancellationToken),
            CheckOneAsync("FFprobe", "ffprobe.exe", ["-version"], cancellationToken)
        };

        return await Task.WhenAll(checks);
    }

    private async Task<ToolHealth> CheckOneAsync(
        string name,
        string fileName,
        string[] args,
        CancellationToken cancellationToken)
    {
        var path = _tools.Find(fileName);
        if (path is null)
        {
            return new ToolHealth(name, string.Empty, false, string.Empty, $"Missing {fileName}");
        }

        try
        {
            var result = await _runner.RunAsync(path, args, cancellationToken);
            var combined = $"{result.StandardOutput}\n{result.StandardError}".Trim();
            var firstLine = combined.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Detected";
            var version = ExtractVersion(firstLine);

            return result.Success
                ? new ToolHealth(name, path, true, version, firstLine)
                : new ToolHealth(name, path, false, version, firstLine);
        }
        catch (Exception ex)
        {
            return new ToolHealth(name, path, false, string.Empty, ex.Message);
        }
    }

    private static string ExtractVersion(string text)
    {
        var match = Regex.Match(text, @"\d+(?:\.\d+){1,3}(?:[-+._a-zA-Z0-9]*)?");
        return match.Success ? match.Value : text.Trim();
    }
}
