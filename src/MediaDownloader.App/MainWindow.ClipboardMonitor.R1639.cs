using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using MediaDownloader.Core.Services;

namespace MediaDownloader;

public partial class MainWindow
{
    private DispatcherTimer? _clipboardMonitorTimerR1639;
    private uint _clipboardSequenceR1639;
    private string _lastClipboardMediaUrlR1639 = string.Empty;
    private bool _clipboardPromptOpenR1639;

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    private void MainWindowClipboardR1639_Loaded(object sender, RoutedEventArgs e)
    {
        _clipboardSequenceR1639 = GetClipboardSequenceNumber();
        _clipboardMonitorTimerR1639 ??= new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(900)
        };
        _clipboardMonitorTimerR1639.Tick -= ClipboardMonitorR1639_Tick;
        _clipboardMonitorTimerR1639.Tick += ClipboardMonitorR1639_Tick;
        _clipboardMonitorTimerR1639.Start();
    }

    private void MainWindowClipboardR1639_Closed(object? sender, EventArgs e)
    {
        _clipboardMonitorTimerR1639?.Stop();
    }

    private void ClipboardMonitorR1639_Tick(object? sender, EventArgs e)
    {
        if (_clipboardPromptOpenR1639 ||
            !ClipboardMediaLinkPreferencesR1639.IsEnabled())
        {
            return;
        }

        var sequence = GetClipboardSequenceNumber();
        if (sequence == 0 || sequence == _clipboardSequenceR1639)
        {
            return;
        }
        _clipboardSequenceR1639 = sequence;

        string text;
        try
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }
            text = Clipboard.GetText().Trim();
        }
        catch
        {
            return;
        }

        if (!ClipboardMediaLinkPreferencesR1639.LooksLikeMediaUrl(text) ||
            string.Equals(text, _lastClipboardMediaUrlR1639, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastClipboardMediaUrlR1639 = text;
        _clipboardPromptOpenR1639 = true;
        try
        {
            if (ClipboardLinkDetectedDialogR1639.ShowFor(this, text) == ClipboardLinkChoiceR1639.Analyze)
            {
                _viewModel.Url = text;
                Activate();
                UrlInputTextBox.Focus();
                UrlInputTextBox.CaretIndex = UrlInputTextBox.Text?.Length ?? 0;
            }
        }
        finally
        {
            _clipboardPromptOpenR1639 = false;
        }
    }
}
