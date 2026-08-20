using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using MediaDownloader.Core.Services;

namespace MediaDownloader;

public partial class App : Application
{
    private static readonly object CrashLogLock = new();
    private static int _fatalUiGate;
    private static Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            base.OnStartup(e);

            if (IsEngineContractTest())
            {
                RunEngineContractTests();
                Shutdown(0);
                return;
            }

            if (!IsNonInteractiveSelfTest() && !AcquireInteractiveSingleInstance())
            {
                Shutdown(0);
                return;
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            WriteCrashLog("Startup", ex);
            HandleFatalUiOnce(ex, "Media Downloader startup error");
            Shutdown(101);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrashLog("DispatcherUnhandledException", e.Exception);
        e.Handled = true;

        // Runtime failures are logged and the process exits without opening another
        // modal error window. A modal dialog from a failing dispatcher can itself
        // recurse while WPF is unwinding, which is exactly the failure mode this
        // release is designed to contain.
        Shutdown(102);
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteCrashLog("AppDomain.UnhandledException", ex);
        }
        else
        {
            WriteCrashLog("AppDomain.UnhandledException", new Exception(e.ExceptionObject?.ToString() ?? "Unknown fatal exception."));
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog("TaskScheduler.UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    private static bool IsStartupSmokeTest() =>
        Environment.GetCommandLineArgs()
            .Any(arg => string.Equals(arg, "--startup-smoke-test", StringComparison.OrdinalIgnoreCase));

    private static bool IsEngineContractTest() =>
        Environment.GetCommandLineArgs()
            .Any(arg => string.Equals(arg, "--engine-contract-test", StringComparison.OrdinalIgnoreCase));

    private static bool IsNonInteractiveSelfTest() => IsStartupSmokeTest() || IsEngineContractTest();

    private static void RunEngineContractTests()
    {
        const string mix = "https://www.youtube.com/watch?v=XTx9FvMLXHk&list=RDXTx9FvMLXHk&start_radio=1";
        const string expectedSingle = "https://www.youtube.com/watch?v=XTx9FvMLXHk";
        var normalizedMix = YtDlpService.NormalizeUserUrl(mix);
        if (!string.Equals(normalizedMix, expectedSingle, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"YouTube Mix normalization failed. Expected '{expectedSingle}', got '{normalizedMix}'.");
        }

        const string watchFromPlaylist = "https://www.youtube.com/watch?v=XTx9FvMLXHk&list=PL12345&index=3";
        var normalizedWatch = YtDlpService.NormalizeUserUrl(watchFromPlaylist);
        if (!string.Equals(normalizedWatch, expectedSingle, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"YouTube watch precedence failed. Expected '{expectedSingle}', got '{normalizedWatch}'.");
        }

        const string explicitPlaylist = "https://www.youtube.com/playlist?list=PL12345";
        var normalizedPlaylist = YtDlpService.NormalizeUserUrl(explicitPlaylist);
        if (!string.Equals(normalizedPlaylist, explicitPlaylist, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Explicit playlist normalization failed. Expected '{explicitPlaylist}', got '{normalizedPlaylist}'.");
        }

        var preferred1080 = YtDlpService.SelectPreferredDefaultQuality(new[]
        {
            new MediaDownloader.Core.Models.QualityChoice(MediaDownloader.Core.Models.QualityChoiceKind.Best, null, "Auto"),
            new MediaDownloader.Core.Models.QualityChoice(MediaDownloader.Core.Models.QualityChoiceKind.ExactHeight, 2160, "2160p", 30),
            new MediaDownloader.Core.Models.QualityChoice(MediaDownloader.Core.Models.QualityChoiceKind.ExactHeight, 1080, "1080p30", 30),
            new MediaDownloader.Core.Models.QualityChoice(MediaDownloader.Core.Models.QualityChoiceKind.ExactHeight, 1080, "1080p60", 60),
            new MediaDownloader.Core.Models.QualityChoice(MediaDownloader.Core.Models.QualityChoiceKind.ExactHeight, 720, "720p60", 60)
        });
        if (preferred1080?.Height != 1080 || preferred1080.Fps != 60)
        {
            throw new InvalidOperationException("Default-quality contract failed: 1080p should be preferred when available.");
        }

        var preferred720 = YtDlpService.SelectPreferredDefaultQuality(new[]
        {
            new MediaDownloader.Core.Models.QualityChoice(MediaDownloader.Core.Models.QualityChoiceKind.Best, null, "Auto"),
            new MediaDownloader.Core.Models.QualityChoice(MediaDownloader.Core.Models.QualityChoiceKind.ExactHeight, 1440, "1440p", 30),
            new MediaDownloader.Core.Models.QualityChoice(MediaDownloader.Core.Models.QualityChoiceKind.ExactHeight, 720, "720p30", 30),
            new MediaDownloader.Core.Models.QualityChoice(MediaDownloader.Core.Models.QualityChoiceKind.ExactHeight, 480, "480p", 30)
        });
        if (preferred720?.Height != 720)
        {
            throw new InvalidOperationException("Default-quality contract failed: 720p should be preferred when 1080p is unavailable.");
        }

        var preferredAudio = YtDlpService.SelectPreferredDefaultAudio(new[]
        {
            new MediaDownloader.Core.Models.AudioChoice("Best available audio"),
            new MediaDownloader.Core.Models.AudioChoice("Opus · 151 kbps · WEBM", "251"),
            new MediaDownloader.Core.Models.AudioChoice("AAC · 128 kbps · M4A", "140")
        });
        if (!string.Equals(preferredAudio?.FormatId, "251", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Default-audio contract failed: the highest concrete audio stream should be selected.");
        }
    }


    private static bool AcquireInteractiveSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(
                initiallyOwned: true,
                name: @"Local\MediaDownloader.Interactive.Singleton",
                createdNew: out var createdNew);
            return createdNew;
        }
        catch
        {
            // If the mutex cannot be created, continue normally rather than
            // preventing the application from starting.
            _singleInstanceMutex = null;
            return true;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
        }
        catch
        {
            // Best effort only.
        }
        finally
        {
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }

    public static string GetCrashLogDirectory()
    {
        var preferred = Path.Combine(AppContext.BaseDirectory, "Logs");
        try
        {
            Directory.CreateDirectory(preferred);
            return preferred;
        }
        catch
        {
            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Media Downloader",
                "Logs");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    public static void WriteCrashLog(string stage, Exception exception)
    {
        try
        {
            var directory = GetCrashLogDirectory();
            var file = Path.Combine(directory, $"App-Crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
            var builder = new StringBuilder();
            builder.AppendLine("Media Downloader application crash report");
            builder.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
            builder.AppendLine($"Stage: {stage}");
            builder.AppendLine($"App base: {AppContext.BaseDirectory}");
            builder.AppendLine($"OS: {Environment.OSVersion}");
            builder.AppendLine($"64-bit process: {Environment.Is64BitProcess}");
            builder.AppendLine();
            builder.AppendLine(exception.ToString());

            lock (CrashLogLock)
            {
                var text = builder.ToString();
                File.WriteAllText(file, text, Encoding.UTF8);
                File.WriteAllText(Path.Combine(directory, "Last-Crash.txt"), text, Encoding.UTF8);
            }
        }
        catch
        {
            // Crash logging must never cause a second crash.
        }
    }

    private static void HandleFatalUiOnce(Exception exception, string title)
    {
        if (IsNonInteractiveSelfTest() || Interlocked.Exchange(ref _fatalUiGate, 1) != 0)
        {
            return;
        }

        try
        {
            MessageBox.Show(
                $"Media Downloader encountered a fatal error.\n\n{exception.Message}\n\nA crash log was written to the Logs folder.",
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // Fatal UI is best effort only. Never create a second error cascade.
        }
    }
}
