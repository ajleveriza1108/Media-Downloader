using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;

namespace MediaDownloader;

public static class BrowserSingleInstanceBridgeR1643
{
    private const string PipeName = "MediaDock.BrowserBridge.R1643";
    private static readonly object Gate = new();
    private static CancellationTokenSource? _serverCancellation;
    private static Task? _serverTask;

    public static void StartServer(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        lock (Gate)
        {
            if (_serverTask is not null)
            {
                return;
            }

            _serverCancellation = new CancellationTokenSource();
            _serverTask = Task.Run(() => RunServerLoopAsync(window, _serverCancellation.Token));
        }
    }

    public static async Task<bool> TryForwardToRunningInstanceAsync(BrowserHandlerRequestR1643 request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(timeout.Token).ConfigureAwait(false);

            var json = JsonSerializer.Serialize(request);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            await client.WriteAsync(bytes, timeout.Token).ConfigureAwait(false);
            await client.FlushAsync(timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void StopServer()
    {
        Task? task;

        lock (Gate)
        {
            _serverCancellation?.Cancel();
            _serverCancellation?.Dispose();
            _serverCancellation = null;
            task = _serverTask;
            _serverTask = null;
        }

        if (task is null)
        {
            return;
        }

        try
        {
            task.Wait(TimeSpan.FromMilliseconds(500));
        }
        catch
        {
        }
    }

    private static async Task RunServerLoopAsync(MainWindow window, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(
                    server,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: true);

                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var request = JsonSerializer.Deserialize<BrowserHandlerRequestR1643>(
                    line,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (request is null ||
                    request.Version != 2 ||
                    !BrowserHandlerRequestR1643.TryNormalizeHttpUrl(request.Url, out var normalizedUrl))
                {
                    continue;
                }

                request = request with { Url = normalizedUrl };

                await window.Dispatcher.InvokeAsync(
                    () => window.AcceptBrowserHandlerRequestR1643(request),
                    DispatcherPriority.Normal,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                App.WriteCrashLog("BrowserBridge.R1643", ex);

                try
                {
                    await Task.Delay(150, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
