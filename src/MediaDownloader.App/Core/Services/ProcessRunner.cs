using System.Diagnostics;
using System.IO;
using System.Text;
using MediaDownloader.Core.Models;

namespace MediaDownloader.Core.Services;

public sealed class ProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default,
        Action<string>? onOutput = null,
        Action<string>? onError = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start {executable}.");
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var callbackGate = new object();
        Exception? callbackException = null;

        void CaptureLine(StringBuilder buffer, string line, Action<string>? callback)
        {
            lock (callbackGate)
            {
                buffer.AppendLine(line);

                if (callbackException is not null || callback is null)
                {
                    return;
                }

                try
                {
                    callback(line);
                }
                catch (Exception ex)
                {
                    callbackException = ex;
                }
            }
        }

        async Task PumpAsync(StreamReader reader, StringBuilder buffer, Action<string>? callback)
        {
            while (true)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                CaptureLine(buffer, line, callback);
            }
        }

        var stdoutPump = PumpAsync(process.StandardOutput, stdout, onOutput);
        var stderrPump = PumpAsync(process.StandardError, stderr, onError);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(stdoutPump, stderrPump).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKillProcessTree(process);
            try
            {
                await Task.WhenAll(stdoutPump, stderrPump).ConfigureAwait(false);
            }
            catch
            {
                // Cancellation remains authoritative.
            }
            throw;
        }

        if (callbackException is not null)
        {
            throw new InvalidOperationException(
                "A native-process progress callback failed safely.",
                callbackException);
        }

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Cancellation is best-effort. The caller still receives cancellation.
        }
    }
}
