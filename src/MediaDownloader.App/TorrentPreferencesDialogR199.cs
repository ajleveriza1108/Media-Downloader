using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using MediaDownloader.Core.Services;
using Microsoft.Win32;

namespace MediaDownloader;

// MEDIADOCK_TORRENT_SETTINGS_NAV_R1910
public sealed class TorrentPreferencesDialogR199 : Window
{
    private static readonly Brush Bg = new SolidColorBrush(Color.FromRgb(15, 18, 22));
    private static readonly Brush Panel = new SolidColorBrush(Color.FromRgb(24, 29, 35));
    private static readonly Brush Panel2 = new SolidColorBrush(Color.FromRgb(20, 25, 31));
    private static readonly Brush Border = new SolidColorBrush(Color.FromRgb(54, 64, 75));
    private static readonly Brush Text = new SolidColorBrush(Color.FromRgb(240, 244, 248));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(174, 187, 201));
    private static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(54, 147, 255));
    private static readonly Brush Selected = new SolidColorBrush(Color.FromRgb(36, 54, 74));

    private readonly Dictionary<string, TextBox> _numbers = new(StringComparer.Ordinal);
    private readonly List<UIElement> _pages = new();
    private ContentControl _content = null!;
    private ListBox _nav = null!;
    private TextBox _downloadDirectory = null!;
    private CheckBox _remember = null!;
    private CheckBox _resume = null!;
    private CheckBox _autoStart = null!;
    private CheckBox _confirmRemove = null!;
    private CheckBox _incoming = null!;
    private CheckBox _portMapping = null!;
    private CheckBox _dht = null!;
    private CheckBox _pex = null!;
    private CheckBox _lpd = null!;
    private CheckBox _fallback = null!;
    private CheckBox _fastDiscovery = null!;
    private CheckBox _fastResume = null!;

    public TorrentPreferencesR199 Settings { get; private set; }

    public TorrentPreferencesDialogR199(Window owner, TorrentPreferencesR199 current)
    {
        Owner = owner;
        Settings = current.Clone();
        Title = "MediaDock Torrent Settings";
        Width = 900;
        Height = 650;
        MinWidth = 820;
        MinHeight = 590;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Bg;
        Foreground = Text;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 12;
        SourceInitialized += (_, _) => ApplyDarkTitleBar();
        Content = BuildUi();
    }

    private void ApplyDarkTitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var enabled = 1;
            if (DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int)) != 0)
                _ = DwmSetWindowAttribute(hwnd, 19, ref enabled, sizeof(int));
        }
        catch { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private UIElement BuildUi()
    {
        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Margin = new Thickness(4, 0, 4, 14) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headerText = new StackPanel();
        headerText.Children.Add(new TextBlock { Text = "Torrent Preferences", Foreground = Text, FontSize = 21, FontWeight = FontWeights.SemiBold });
        headerText.Children.Add(new TextBlock { Text = "Persistent queue, fast peer discovery, connection, bandwidth and BitTorrent behavior", Foreground = Muted, Margin = new Thickness(0, 3, 0, 0) });
        header.Children.Add(headerText);
        var fastBadge = new Border { Background = Selected, BorderBrush = Accent, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 5, 10, 5), VerticalAlignment = VerticalAlignment.Center };
        fastBadge.Child = new TextBlock { Text = "FAST peer discovery", Foreground = Text, FontWeight = FontWeights.SemiBold };
        Grid.SetColumn(fastBadge, 1);
        header.Children.Add(fastBadge);
        root.Children.Add(header);

        var body = new Grid();
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(185) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        _nav = new ListBox
        {
            Background = Panel2,
            Foreground = Text,
            BorderBrush = Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Text));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 9, 10, 9)));
        itemStyle.Setters.Add(new Setter(Control.MarginProperty, new Thickness(0, 1, 0, 1)));
        itemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
        var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
        selectedTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Selected));
        selectedTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        selectedTrigger.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        itemStyle.Triggers.Add(selectedTrigger);
        _nav.ItemContainerStyle = itemStyle;
        foreach (var title in new[] { "General", "Directories", "Connection", "Bandwidth", "BitTorrent", "Queueing", "Advanced" })
            _nav.Items.Add(title);
        _nav.SelectionChanged += (_, _) => ShowSelectedPage();
        body.Children.Add(_nav);

        var contentBorder = new Border { Background = Panel, BorderBrush = Border, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(2) };
        _content = new ContentControl();
        contentBorder.Child = _content;
        Grid.SetColumn(contentBorder, 2);
        body.Children.Add(contentBorder);

        _pages.Add(BuildGeneral());
        _pages.Add(BuildDirectories());
        _pages.Add(BuildConnection());
        _pages.Add(BuildBandwidth());
        _pages.Add(BuildBitTorrent());
        _pages.Add(BuildQueueing());
        _pages.Add(BuildAdvanced());
        _nav.SelectedIndex = 0;

        var buttons = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var defaults = MakeButton("Fast defaults", (_, _) => LoadFastDefaults());
        buttons.Children.Add(defaults);
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        right.Children.Add(MakeButton("Cancel", (_, _) => { DialogResult = false; Close(); }));
        right.Children.Add(MakeButton("Apply", (_, _) => ApplyAndClose(), primary: true));
        Grid.SetColumn(right, 1);
        buttons.Children.Add(right);
        Grid.SetRow(buttons, 2);
        root.Children.Add(buttons);
        return root;
    }

    private void ShowSelectedPage()
    {
        var index = _nav.SelectedIndex;
        if (index < 0 || index >= _pages.Count) return;
        _content.Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _pages[index]
        };
    }

    private UIElement BuildGeneral()
    {
        var panel = Page("General", "Startup, persistence and list behavior");
        _remember = AddCheck(panel, "Keep loaded torrents in MediaDock after restart", Settings.RememberLoadedTorrents);
        _resume = AddCheck(panel, "Resume torrents that were running when MediaDock closed", Settings.ResumeLoadedTorrents);
        _autoStart = AddCheck(panel, "Start new torrents automatically", Settings.AutoStartDownloads);
        _confirmRemove = AddCheck(panel, "Confirm before removing torrents", Settings.ConfirmRemove);
        AddNote(panel, "Loaded torrents are stored in %LOCALAPPDATA%\\AJCoder\\MediaDock\\TorrentClient and remain in the app until you remove them.");
        return panel;
    }

    private UIElement BuildDirectories()
    {
        var panel = Page("Directories", "Default location for new torrent data");
        var row = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _downloadDirectory = MakeText(Settings.DownloadDirectory);
        row.Children.Add(_downloadDirectory);
        var browse = MakeButton("Browse…", (_, _) => BrowseFolder());
        Grid.SetColumn(browse, 1);
        row.Children.Add(browse);
        panel.Children.Add(row);
        AddNote(panel, "Existing loaded torrents keep their saved location. New torrents use this folder automatically.");
        return panel;
    }

    private UIElement BuildConnection()
    {
        var panel = Page("Connection", "Listening, DHT UDP and router mapping");
        _incoming = AddCheck(panel, "Enable incoming peer connections", Settings.EnableIncomingConnections);
        AddNumber(panel, "TCP listening port (0 = automatic)", "ListeningPort", Settings.ListeningPort);
        AddNumber(panel, "DHT UDP port (0 = automatic)", "DhtPort", Settings.DhtPort);
        _portMapping = AddCheck(panel, "Enable UPnP / NAT-PMP port mapping", Settings.EnablePortMapping);
        AddNote(panel, "MediaDock uses outbound peers even when incoming connections are disabled. Enabling incoming connections can improve swarm reachability, but Windows may show a one-time firewall permission prompt for the installed TorrentHost.");
        return panel;
    }

    private UIElement BuildBandwidth()
    {
        var panel = Page("Bandwidth", "Global transfer and peer-connection limits");
        AddNumber(panel, "Maximum download rate (KB/s, 0 = unlimited)", "Download", Settings.MaximumDownloadRateKbps);
        AddNumber(panel, "Maximum upload rate (KB/s, 0 = unlimited)", "Upload", Settings.MaximumUploadRateKbps);
        AddNumber(panel, "Global maximum connections", "Connections", Settings.MaximumConnections);
        AddNumber(panel, "Maximum half-open connections", "HalfOpen", Settings.MaximumHalfOpenConnections);
        AddNumber(panel, "Maximum peers per torrent", "Peers", Settings.MaximumPeersPerTorrent);
        AddNumber(panel, "Upload slots per torrent", "Slots", Settings.UploadSlotsPerTorrent);
        return panel;
    }

    private UIElement BuildBitTorrent()
    {
        var panel = Page("BitTorrent", "Peer discovery and swarm bootstrap");
        _fastDiscovery = AddCheck(panel, "Fast peer discovery at torrent start", Settings.FastPeerDiscovery);
        _dht = AddCheck(panel, "Enable DHT network", Settings.EnableDht);
        _pex = AddCheck(panel, "Enable Peer Exchange (PEX)", Settings.EnablePex);
        _lpd = AddCheck(panel, "Enable Local Peer Discovery", Settings.EnableLocalPeerDiscovery);
        _fallback = AddCheck(panel, "Immediately add public fallback trackers to public torrents", Settings.UsePublicTrackerFallback);
        AddNumber(panel, "Peer recovery attempts", "RecoveryAttempts", Settings.PeerRecoveryAttempts);
        AddNumber(panel, "Seconds between later recovery attempts", "RecoverySeconds", Settings.PeerRecoveryIntervalSeconds);
        AddNote(panel, "Fast peer discovery announces trackers, DHT and local discovery immediately, then performs short recovery retries. Private torrents never receive public fallback trackers.");
        return panel;
    }

    private UIElement BuildQueueing()
    {
        var panel = Page("Queueing", "Torrent priority and active download slots");
        AddNumber(panel, "Maximum active downloads", "ActiveDownloads", Settings.MaximumActiveDownloads);
        AddNote(panel, "Torrent order in the main list is the queue priority. Use ↑ Priority / ↓ Priority or the context menu. Queue order is saved between launches.");
        return panel;
    }

    private UIElement BuildAdvanced()
    {
        var panel = Page("Advanced", "Safe performance defaults");
        _fastResume = AddCheck(panel, "Enable fast-resume cache", Settings.EnableFastResume);
        AddNote(panel, "Fast resume stores verified piece state in MediaDock's stable local cache so existing torrents do not repeat unnecessary startup work. If a torrent becomes inconsistent, use Force Recheck from the Torrent workspace.");
        return panel;
    }

    private StackPanel Page(string title, string subtitle)
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = title, Foreground = Text, FontSize = 18, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = subtitle, Foreground = Muted, Margin = new Thickness(0, 3, 0, 16) });
        return panel;
    }

    private CheckBox AddCheck(Panel parent, string label, bool value)
    {
        var box = new CheckBox { Content = label, IsChecked = value, Foreground = Text, Margin = new Thickness(0, 6, 0, 6), VerticalContentAlignment = VerticalAlignment.Center };
        parent.Children.Add(box);
        return box;
    }

    private void AddNumber(Panel parent, string label, string key, int value)
    {
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.Children.Add(new TextBlock { Text = label, Foreground = Text, VerticalAlignment = VerticalAlignment.Center });
        var box = MakeText(value.ToString());
        Grid.SetColumn(box, 1);
        grid.Children.Add(box);
        _numbers[key] = box;
        parent.Children.Add(grid);
    }

    private static TextBox MakeText(string text) => new()
    {
        Text = text,
        MinHeight = 32,
        Padding = new Thickness(9, 5, 9, 5),
        Background = new SolidColorBrush(Color.FromRgb(11, 15, 19)),
        Foreground = Text,
        BorderBrush = Border,
        CaretBrush = Text,
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 12
    };

    private void AddNote(Panel panel, string text) => panel.Children.Add(new TextBlock
    {
        Text = text,
        Foreground = Muted,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 10, 0, 4),
        LineHeight = 18
    });

    private Button MakeButton(string text, RoutedEventHandler click, bool primary = false)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 104,
            MinHeight = 36,
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(14, 6, 14, 6),
            Foreground = Brushes.White,
            Background = primary ? Accent : Panel2,
            BorderBrush = primary ? Accent : Border,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold
        };
        button.Click += click;
        return button;
    }

    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Choose torrent download folder", InitialDirectory = _downloadDirectory.Text };
        if (dialog.ShowDialog(this) == true) _downloadDirectory.Text = dialog.FolderName;
    }

    private int Number(string key, int fallback)
        => _numbers.TryGetValue(key, out var box) && int.TryParse(box.Text.Trim(), out var value) ? value : fallback;

    private void ApplyAndClose()
    {
        var result = Settings.Clone();
        result.RememberLoadedTorrents = _remember.IsChecked == true;
        result.ResumeLoadedTorrents = _resume.IsChecked == true;
        result.AutoStartDownloads = _autoStart.IsChecked == true;
        result.ConfirmRemove = _confirmRemove.IsChecked == true;
        result.EnableIncomingConnections = _incoming.IsChecked == true;
        result.ListeningPort = Number("ListeningPort", result.ListeningPort);
        result.DhtPort = Number("DhtPort", result.DhtPort);
        result.EnablePortMapping = _portMapping.IsChecked == true;
        result.EnableDht = _dht.IsChecked == true;
        result.EnablePex = _pex.IsChecked == true;
        result.EnableLocalPeerDiscovery = _lpd.IsChecked == true;
        result.UsePublicTrackerFallback = _fallback.IsChecked == true;
        result.FastPeerDiscovery = _fastDiscovery.IsChecked == true;
        result.EnableFastResume = _fastResume.IsChecked == true;
        result.MaximumDownloadRateKbps = Number("Download", result.MaximumDownloadRateKbps);
        result.MaximumUploadRateKbps = Number("Upload", result.MaximumUploadRateKbps);
        result.MaximumConnections = Number("Connections", result.MaximumConnections);
        result.MaximumHalfOpenConnections = Number("HalfOpen", result.MaximumHalfOpenConnections);
        result.MaximumPeersPerTorrent = Number("Peers", result.MaximumPeersPerTorrent);
        result.UploadSlotsPerTorrent = Number("Slots", result.UploadSlotsPerTorrent);
        result.MaximumActiveDownloads = Number("ActiveDownloads", result.MaximumActiveDownloads);
        result.PeerRecoveryAttempts = Number("RecoveryAttempts", result.PeerRecoveryAttempts);
        result.PeerRecoveryIntervalSeconds = Number("RecoverySeconds", result.PeerRecoveryIntervalSeconds);
        result.DownloadDirectory = _downloadDirectory.Text.Trim();
        Settings = result;
        DialogResult = true;
        Close();
    }

    private void LoadFastDefaults()
    {
        _autoStart.IsChecked = true;
        _fastDiscovery.IsChecked = true;
        _fastResume.IsChecked = true;
        _dht.IsChecked = true;
        _pex.IsChecked = true;
        _lpd.IsChecked = true;
        _fallback.IsChecked = true;
        _numbers["DhtPort"].Text = "0";
        _numbers["Download"].Text = "0";
        _numbers["Upload"].Text = "0";
        _numbers["Connections"].Text = "600";
        _numbers["HalfOpen"].Text = "64";
        _numbers["Peers"].Text = "300";
        _numbers["Slots"].Text = "16";
        _numbers["ActiveDownloads"].Text = "3";
        _numbers["RecoveryAttempts"].Text = "24";
        _numbers["RecoverySeconds"].Text = "3";
    }
}
