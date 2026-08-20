using System.IO;

namespace MediaDownloader.Core.Services;

public sealed class FfmpegConversionService
{
    private readonly ToolLocator _tools;
    private readonly ProcessRunner _runner;

    public FfmpegConversionService(ToolLocator tools, ProcessRunner runner)
    {
        _tools = tools;
        _runner = runner;
    }

    public async Task<string> ConvertToMp3Async(
        string inputPath,
        string outputDirectory,
        int bitrateKbps,
        CancellationToken cancellationToken = default,
        Action<string>? onOutput = null,
        Action<string>? onError = null)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            throw new InvalidOperationException("The selected input file was not found.");
        }

        Directory.CreateDirectory(outputDirectory);

        var ffmpeg = _tools.Find("ffmpeg.exe") ?? throw new InvalidOperationException("Required tool ffmpeg.exe was not found in the Tools folder or PATH.");
        var outputPath = BuildUniqueMp3Path(outputDirectory, Path.GetFileNameWithoutExtension(inputPath));

        var args = new List<string>
        {
            "-y",
            "-i", inputPath,
            "-vn",
            "-map_metadata", "0",
            "-c:a", "libmp3lame",
            "-b:a", string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}k", bitrateKbps),
            outputPath
        };

        var result = await _runner.RunAsync(ffmpeg, args, cancellationToken, onOutput, onError);
        if (!result.Success || !File.Exists(outputPath))
        {
            var diagnostics = string.Concat(result.StandardOutput, Environment.NewLine, result.StandardError).Trim();
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(diagnostics)
                ? "FFmpeg conversion to MP3 failed."
                : diagnostics);
        }

        return outputPath;
    }

    private static string BuildUniqueMp3Path(string outputDirectory, string baseName)
    {
        var cleanName = string.IsNullOrWhiteSpace(baseName) ? "Converted Audio" : baseName;
        var outputPath = Path.Combine(outputDirectory, cleanName + ".mp3");
        var counter = 2;
        while (File.Exists(outputPath))
        {
            outputPath = Path.Combine(outputDirectory, string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} ({1}).mp3", cleanName, counter));
            counter++;
        }

        return outputPath;
    }
}
