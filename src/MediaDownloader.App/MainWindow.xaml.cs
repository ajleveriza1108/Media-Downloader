using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using MediaDownloader.ViewModels;

namespace MediaDownloader;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly bool _startupSmokeTest;
    private bool _applyingResponsiveLayout;
    private bool _responsiveLayoutQueued;

    public MainWindow()
    {
        _startupSmokeTest = Environment.GetCommandLineArgs()
            .Any(arg => string.Equals(arg, "--startup-smoke-test", StringComparison.OrdinalIgnoreCase));

        InitializeComponent();
        DataContext = _viewModel;

        if (_startupSmokeTest)
        {
            // Reproduce the user-reported short/narrow viewport during the automated
            // Windows GUI smoke gate. The footer must remain visible at scroll offset 0.
            Width = 935;
            Height = 485;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = 24;
            Top = 24;
        }

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        ContentRendered += OnContentRendered;
    }

    private const int WmGetMinMaxInfo = 0x0024;
    private const uint MonitorDefaultToNearest = 0x00000002;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;

        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WindowMessageHook);
    }

    private static IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmGetMinMaxInfo)
        {
            ApplyMonitorWorkingArea(hwnd, lParam);
        }

        return IntPtr.Zero;
    }

    private static void ApplyMonitorWorkingArea(IntPtr hwnd, IntPtr lParam)
    {
        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var work = monitorInfo.WorkArea;
        var bounds = monitorInfo.MonitorArea;

        minMaxInfo.MaxPosition.X = work.Left - bounds.Left;
        minMaxInfo.MaxPosition.Y = work.Top - bounds.Top;
        minMaxInfo.MaxSize.X = work.Right - work.Left;
        minMaxInfo.MaxSize.Y = work.Bottom - work.Top;
        minMaxInfo.MaxTrackSize = minMaxInfo.MaxSize;

        Marshal.StructureToPtr(minMaxInfo, lParam, false);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        ApplyResponsiveLayout(MainContentRoot.ActualWidth, MainContentRoot.ActualHeight);

        if (_startupSmokeTest)
        {
            return;
        }

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            App.WriteCrashLog("InitialToolHealthRefresh", ex);
            _viewModel.ReportStartupWarning("The app opened, but the initial tool-health refresh failed. Open Diagnostics from the gear button.", ex);
        }
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnContentRendered;

        if (!_startupSmokeTest)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(RunStartupLayoutSmokeTest));
    }

    private void RunStartupLayoutSmokeTest()
    {
        try
        {
            ApplyResponsiveLayout(MainContentRoot.ActualWidth, MainContentRoot.ActualHeight);
            UpdateLayout();

            if (PanelsScrollViewer.ActualWidth <= 0 || PanelsScrollViewer.ActualHeight <= 0 ||
                MediaPanel.ActualWidth <= 0 || MediaPanel.ActualHeight <= 0 ||
                DownloadFooterGrid.ActualWidth <= 0 || DownloadFooterGrid.ActualHeight <= 0)
            {
                throw new InvalidOperationException("Responsive layout smoke test found a zero-sized required surface.");
            }

            var footerBottom = DownloadFooterGrid
                .TransformToAncestor(PanelsScrollViewer)
                .Transform(new Point(0, DownloadFooterGrid.ActualHeight))
                .Y;

            if (footerBottom > PanelsScrollViewer.ViewportHeight + 1)
            {
                throw new InvalidOperationException(
                    $"Download footer is below the initial viewport at 935x485. FooterBottom={footerBottom:0.0}, ViewportHeight={PanelsScrollViewer.ViewportHeight:0.0}.");
            }

            if (MediaPanel.ActualWidth > PanelsScrollViewer.ViewportWidth + 1)
            {
                throw new InvalidOperationException(
                    $"Media panel exceeds the responsive viewport width. MediaWidth={MediaPanel.ActualWidth:0.0}, ViewportWidth={PanelsScrollViewer.ViewportWidth:0.0}.");
            }

            if (PanelsScrollViewer.ScrollableHeight > 0 &&
                PanelsScrollViewer.ComputedVerticalScrollBarVisibility != Visibility.Visible)
            {
                throw new InvalidOperationException("Scrollable content exists but the vertical scrollbar is not visible.");
            }

            AssertCompactMediaCardHasNoTrailingVoid("935x485");
            AssertLeftPanelNoCropNoHide("935x485");

            // Reproduce the user's latest 958x852 screenshot. At this width the layout must
            // use Media on top with Queue/Convert immediately below, not one giant Media card.
            Width = 958;
            Height = 852;
            UpdateLayout();
            ApplyResponsiveLayout(MainContentRoot.ActualWidth, MainContentRoot.ActualHeight);
            UpdateLayout();

            AssertCompactMediaCardHasNoTrailingVoid("958x852");
            AssertLeftPanelNoCropNoHide("958x852");

            // Reproduce the latest wide/maximized screenshot geometry too.
            Width = 1906;
            Height = 1013;
            UpdateLayout();
            ApplyResponsiveLayout(MainContentRoot.ActualWidth, MainContentRoot.ActualHeight);
            UpdateLayout();
            AssertLeftPanelNoCropNoHide("1906x1013");
            AssertCompactMediaCardHasNoTrailingVoid("1906x1013");

            Width = 958;
            Height = 852;
            UpdateLayout();
            ApplyResponsiveLayout(MainContentRoot.ActualWidth, MainContentRoot.ActualHeight);
            UpdateLayout();

            var mediaBottom = MediaPanel.TransformToAncestor(PanelsGrid)
                .Transform(new Point(0, MediaPanel.ActualHeight)).Y;
            var queueTop = QueuePanel.TransformToAncestor(PanelsGrid)
                .Transform(new Point(0, 0)).Y;
            var gap = queueTop - mediaBottom;
            if (gap < 0 || gap > 20)
            {
                throw new InvalidOperationException(
                    $"Medium layout left an invalid gap after Media at 958x852. Gap={gap:0.0}.");
            }

            Application.Current.Shutdown(0);
        }
        catch (Exception ex)
        {
            App.WriteCrashLog("StartupLayoutSmokeTest", ex);
            Application.Current.Shutdown(1);
        }
    }

    private void AssertUrlInputHasSafeTextViewport(string label)
    {
        UrlInputTextBox.ApplyTemplate();

        if (UrlInputTextBox.Visibility != Visibility.Visible ||
            UrlInputTextBox.ActualHeight < 38 ||
            UrlInputTextBox.FontSize < 15)
        {
            throw new InvalidOperationException(
                $"URL input is hidden or undersized at {label}. Visibility={UrlInputTextBox.Visibility}, Height={UrlInputTextBox.ActualHeight:0.0}, FontSize={UrlInputTextBox.FontSize:0.0}.");
        }

        var host = UrlInputTextBox.Template.FindName("PART_ContentHost", UrlInputTextBox) as FrameworkElement;
        if (host is null || host.ActualHeight < UrlInputTextBox.FontSize + 6)
        {
            throw new InvalidOperationException(
                $"URL input content host is too short for unclipped glyphs at {label}. HostHeight={(host is null ? 0 : host.ActualHeight):0.0}, FontSize={UrlInputTextBox.FontSize:0.0}.");
        }
    }

    private void AssertLeftPanelElementInsideViewport(FrameworkElement element, string name, string label)
    {
        if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            throw new InvalidOperationException(
                $"Left-panel element '{name}' is hidden or zero-sized at {label}. Visibility={element.Visibility}, Size={element.ActualWidth:0.0}x{element.ActualHeight:0.0}.");
        }

        var topLeft = element.TransformToAncestor(MediaPanel).Transform(new Point(0, 0));
        var bottomRight = element.TransformToAncestor(MediaPanel)
            .Transform(new Point(element.ActualWidth, element.ActualHeight));

        const double tolerance = 2.0;
        if (topLeft.X < -tolerance || topLeft.Y < -tolerance ||
            bottomRight.X > MediaPanel.ActualWidth + tolerance ||
            bottomRight.Y > MediaPanel.ActualHeight + tolerance)
        {
            throw new InvalidOperationException(
                $"Left-panel element '{name}' is clipped at {label}. Bounds=({topLeft.X:0.0},{topLeft.Y:0.0})-({bottomRight.X:0.0},{bottomRight.Y:0.0}), Media={MediaPanel.ActualWidth:0.0}x{MediaPanel.ActualHeight:0.0}.");
        }
    }

    private void AssertLeftPanelNoCropNoHide(string label)
    {
        AssertUrlInputHasSafeTextViewport(label);
        AssertLeftPanelElementInsideViewport(MediaTitleText, nameof(MediaTitleText), label);
        AssertLeftPanelElementInsideViewport(MediaPlatformRow, nameof(MediaPlatformRow), label);
        AssertLeftPanelElementInsideViewport(MediaUploaderText, nameof(MediaUploaderText), label);
        AssertLeftPanelElementInsideViewport(MediaFormatsText, nameof(MediaFormatsText), label);
        AssertLeftPanelElementInsideViewport(DownloadOptionsCard, nameof(DownloadOptionsCard), label);
        AssertLeftPanelElementInsideViewport(DownloadFooterGrid, nameof(DownloadFooterGrid), label);

        if (MediaTitleText.MaxHeight < double.PositiveInfinity)
        {
            throw new InvalidOperationException($"Media title still has a clipping MaxHeight at {label}.");
        }

        if (UrlStatusText.Visibility != Visibility.Visible || MediaUploaderText.Visibility != Visibility.Visible)
        {
            throw new InvalidOperationException($"Responsive layout deliberately hid required status/uploader text at {label}.");
        }
    }

    private void AssertCompactMediaCardHasNoTrailingVoid(string label)
    {
        var footerBottom = DownloadFooterGrid
            .TransformToAncestor(MediaPanel)
            .Transform(new Point(0, DownloadFooterGrid.ActualHeight))
            .Y;
        var trailingSpace = MediaPanel.ActualHeight - footerBottom - MediaPanel.Padding.Bottom;

        if (trailingSpace > 28)
        {
            throw new InvalidOperationException(
                $"Media card reserved blank trailing height at {label}. TrailingSpace={trailingSpace:0.0}, MediaHeight={MediaPanel.ActualHeight:0.0}.");
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_responsiveLayoutQueued)
        {
            return;
        }

        _responsiveLayoutQueued = true;
        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                _responsiveLayoutQueued = false;
                if (!IsLoaded)
                {
                    return;
                }

                ApplyResponsiveLayout(MainContentRoot.ActualWidth, MainContentRoot.ActualHeight);
            }));
    }

    private void ApplyResponsiveLayout(double availableWidth, double availableHeight)
    {
        if (_applyingResponsiveLayout)
        {
            return;
        }

        if (PanelsGrid is null || PanelsScrollViewer is null || MediaPanel is null || QueuePanel is null ||
            ConvertPanel is null || UrlStatusText is null || MediaThumbnailColumn is null ||
            MediaThumbnailSpacerColumn is null || MediaThumbnailHost is null || MediaTitleText is null ||
            MediaPlatformRow is null || MediaUploaderText is null || MediaFormatsText is null ||
            DownloadOptionsCard is null || DownloadChoiceGrid is null || QualityChoicePanel is null ||
            AudioChoicePanel is null || Mp3BitratePanel is null || DownloadFooterGrid is null ||
            DownloadFooterActions is null)
        {
            return;
        }

        _applyingResponsiveLayout = true;
        try
        {
        // Three deterministic layout bands keep the desktop dense without ever making
        // the Media card consume unused viewport height:
        //   >=1180 : Media | Queue | Convert
        //   760-1179: Media full-width, Queue | Convert below
        //   <760    : Media, Queue, Convert stacked
        // Height only compresses the summary; overflow belongs to PanelsScrollViewer.
        var wideLayout = availableWidth >= 1180;
        var mediumLayout = availableWidth >= 760;
        var shortHeight = availableHeight < 620;
        var veryShortHeight = availableHeight < 500;

        // R1.5.9.5: never deliberately hide informational UI to make the layout fit.
        // Reflow/wrap first; outer scrolling remains the final fallback.
        UrlStatusText.Visibility = Visibility.Visible;
        MediaUploaderText.Visibility = Visibility.Visible;
        PanelsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        PanelsScrollViewer.VerticalContentAlignment = VerticalAlignment.Top;

        PanelsGrid.Height = double.NaN;
        PanelsGrid.MinHeight = 0;
        PanelsGrid.VerticalAlignment = VerticalAlignment.Top;
        PanelsGrid.ColumnDefinitions.Clear();
        PanelsGrid.RowDefinitions.Clear();

        foreach (var panel in new FrameworkElement[] { MediaPanel, QueuePanel, ConvertPanel })
        {
            panel.Height = double.NaN;
            panel.MinHeight = 0;
            panel.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetRow(panel, 0);
            Grid.SetColumn(panel, 0);
            Grid.SetRowSpan(panel, 1);
            Grid.SetColumnSpan(panel, 1);
        }

        if (wideLayout)
        {
            PanelsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            PanelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.05, GridUnitType.Star) });
            PanelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            PanelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            PanelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            PanelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.78, GridUnitType.Star) });

            Grid.SetColumn(MediaPanel, 0);
            Grid.SetColumn(QueuePanel, 2);
            Grid.SetColumn(ConvertPanel, 4);

            // Queue/Convert use the actual scroll viewport, not MainContentRoot height.
            // This avoids manufacturing a vertical scrollbar just because the URL strip exists.
            var panelViewportHeight = PanelsScrollViewer.ActualHeight > 0
                ? PanelsScrollViewer.ActualHeight
                : Math.Max(360, availableHeight - 60);
            QueuePanel.MinHeight = Math.Max(360, panelViewportHeight - 2);
            ConvertPanel.MinHeight = Math.Max(360, panelViewportHeight - 2);
        }
        else if (mediumLayout)
        {
            PanelsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            PanelsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            PanelsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            PanelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            PanelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            PanelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(MediaPanel, 0);
            Grid.SetColumn(MediaPanel, 0);
            Grid.SetColumnSpan(MediaPanel, 3);

            Grid.SetRow(QueuePanel, 2);
            Grid.SetColumn(QueuePanel, 0);
            Grid.SetRow(ConvertPanel, 2);
            Grid.SetColumn(ConvertPanel, 2);

            QueuePanel.MinHeight = shortHeight ? 180 : 240;
            ConvertPanel.MinHeight = shortHeight ? 260 : 310;
        }
        else
        {
            PanelsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            PanelsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            PanelsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            PanelsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            PanelsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            PanelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(MediaPanel, 0);
            Grid.SetRow(QueuePanel, 2);
            Grid.SetRow(ConvertPanel, 4);

            QueuePanel.MinHeight = shortHeight ? 180 : 220;
            ConvertPanel.MinHeight = shortHeight ? 260 : 310;
        }

        // Media summary density is controlled independently from panel placement.
        var compactMedia = !wideLayout;
        var thumbnailWidth = veryShortHeight ? 88 : shortHeight ? 104 : compactMedia ? 120 : 150;
        var thumbnailHeight = veryShortHeight ? 76 : shortHeight ? 92 : compactMedia ? 108 : 138;

        MediaThumbnailColumn.Width = new GridLength(thumbnailWidth);
        MediaThumbnailSpacerColumn.Width = new GridLength(compactMedia ? 10 : 14);
        MediaThumbnailHost.Height = thumbnailHeight;
        MediaPanel.Padding = new Thickness(veryShortHeight ? 9 : compactMedia ? 10 : 14);
        MediaTitleText.FontSize = veryShortHeight ? 15 : shortHeight ? 16 : compactMedia ? 17 : 18;
        MediaTitleText.MaxHeight = double.PositiveInfinity;
        MediaPlatformRow.Margin = new Thickness(0, veryShortHeight ? 4 : compactMedia ? 5 : 6, 0, 0);
        MediaUploaderText.Visibility = Visibility.Visible;
        MediaFormatsText.Margin = new Thickness(0, veryShortHeight ? 4 : compactMedia ? 6 : 8, 0, 0);
        MediaFormatsText.FontSize = veryShortHeight ? 10 : compactMedia ? 11 : 12;
        DownloadOptionsCard.Padding = new Thickness(veryShortHeight ? 7 : 8);

        // Internal left-panel reflow is based on the panel itself, not only the window.
        // At narrow panel widths the two option fields stack instead of being clipped.
        var mediaInnerWidth = MediaPanel.ActualWidth > 0 ? MediaPanel.ActualWidth : availableWidth;
        var stackDownloadChoices = mediaInnerWidth < 660;

        DownloadChoiceGrid.RowDefinitions[1].Height = stackDownloadChoices
            ? new GridLength(8)
            : new GridLength(0);
        DownloadChoiceGrid.RowDefinitions[3].Height = stackDownloadChoices
            ? new GridLength(8)
            : new GridLength(0);

        if (stackDownloadChoices)
        {
            DownloadChoiceGrid.ColumnDefinitions[1].Width = new GridLength(0);

            Grid.SetRow(QualityChoicePanel, 0);
            Grid.SetColumn(QualityChoicePanel, 0);
            Grid.SetColumnSpan(QualityChoicePanel, 3);

            Grid.SetRow(AudioChoicePanel, 2);
            Grid.SetColumn(AudioChoicePanel, 0);
            Grid.SetColumnSpan(AudioChoicePanel, 3);

            Grid.SetRow(Mp3BitratePanel, 4);
            Grid.SetColumn(Mp3BitratePanel, 0);
            Grid.SetColumnSpan(Mp3BitratePanel, 3);
        }
        else
        {
            DownloadChoiceGrid.ColumnDefinitions[1].Width = new GridLength(10);

            Grid.SetRow(QualityChoicePanel, 0);
            Grid.SetColumn(QualityChoicePanel, 0);
            Grid.SetColumnSpan(QualityChoicePanel, 1);

            Grid.SetRow(AudioChoicePanel, 0);
            AudioChoicePanel.ClearValue(Grid.ColumnProperty);
            Grid.SetColumnSpan(AudioChoicePanel, 1);

            Grid.SetRow(Mp3BitratePanel, 0);
            Grid.SetColumn(Mp3BitratePanel, 2);
            Grid.SetColumnSpan(Mp3BitratePanel, 1);
        }
        }
        finally
        {
            _applyingResponsiveLayout = false;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximizeRestore();
            return;
        }

        try
        {
            DragMove();
        }
        catch
        {
            // Ignore drag race when the mouse is released during DragMove.
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) => ToggleMaximizeRestore();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximizeRestore()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OpenDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var diagnostics = new DiagnosticsWindow(_viewModel)
        {
            Owner = this
        };
        diagnostics.ShowDialog();
    }

    private void ConvertDropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void ConvertDropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            _viewModel.SetConversionInputPath(files[0]);
        }
    }
}
