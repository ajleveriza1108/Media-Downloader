using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaDownloader.Core.Services;

namespace MediaDownloader;

internal static class TorrentDialogThemeR194
{
    private static readonly Brush FallbackPanel = new SolidColorBrush(Color.FromRgb(15, 18, 22));
    private static readonly Brush FallbackSurface = new SolidColorBrush(Color.FromRgb(27, 32, 39));
    private static readonly Brush FallbackPrimary = new SolidColorBrush(Color.FromRgb(238, 242, 247));
    private static readonly Brush FallbackSecondary = new SolidColorBrush(Color.FromRgb(183, 194, 207));
    private static readonly Brush FallbackBorder = new SolidColorBrush(Color.FromRgb(52, 61, 72));

    public static void Apply(Window window, Window owner)
    {
        window.FontFamily = owner.FontFamily;
        window.Background = ResolveBrush(owner, "ThemePanelBackgroundBrush", FallbackPanel);
        window.Foreground = ResolveBrush(owner, "ThemePrimaryTextBrush", FallbackPrimary);
    }

    public static void ApplyGrid(DataGrid grid, Window owner)
    {
        var panel = ResolveBrush(owner, "ThemePanelBackgroundBrush", FallbackPanel);
        var surface = ResolveBrush(owner, "ThemeSurfaceBrush", FallbackSurface);
        var primary = ResolveBrush(owner, "ThemePrimaryTextBrush", FallbackPrimary);
        var secondary = ResolveBrush(owner, "ThemeSecondaryTextBrush", FallbackSecondary);
        var border = ResolveBrush(owner, "ThemeBorderBrush", FallbackBorder);
        var accent = ResolveBrush(owner, "ThemeAccentBrush", SystemColors.HighlightBrush);

        grid.Background = panel;
        grid.Foreground = primary;
        grid.BorderBrush = border;
        grid.HorizontalGridLinesBrush = border;
        grid.VerticalGridLinesBrush = border;
        grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        grid.RowHeaderWidth = 0;
        grid.RowHeight = 32;
        grid.ColumnHeaderHeight = 32;
        grid.RowBackground = panel;
        grid.AlternatingRowBackground = surface;
        grid.AlternationCount = 2;

        var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(Control.BackgroundProperty, surface));
        headerStyle.Setters.Add(new Setter(Control.ForegroundProperty, secondary));
        headerStyle.Setters.Add(new Setter(Control.BorderBrushProperty, border));
        headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 0, 8, 0)));
        headerStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        grid.ColumnHeaderStyle = headerStyle;

        var rowStyle = new Style(typeof(DataGridRow));
        rowStyle.Setters.Add(new Setter(Control.ForegroundProperty, primary));
        var selected = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(Control.BackgroundProperty, accent));
        selected.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        rowStyle.Triggers.Add(selected);
        grid.RowStyle = rowStyle;
    }

    public static DataGridTextColumn CreateTextColumn(
        Window owner,
        string header,
        string propertyName,
        DataGridLength width,
        bool secondary = false)
    {
        var foreground = ResolveBrush(
            owner,
            secondary ? "ThemeSecondaryTextBrush" : "ThemePrimaryTextBrush",
            secondary ? FallbackSecondary : FallbackPrimary);
        var elementStyle = new Style(typeof(TextBlock));
        elementStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, foreground));
        elementStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI")));
        elementStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 10.5d));
        elementStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        elementStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));

        return new DataGridTextColumn
        {
            Header = header,
            Binding = new System.Windows.Data.Binding(propertyName),
            Width = width,
            IsReadOnly = true,
            ElementStyle = elementStyle
        };
    }

    public static void ApplyButton(Button button, Window owner, string styleKey)
    {
        if (owner.TryFindResource(styleKey) is Style style)
        {
            button.Style = style;
        }
    }

    private static Brush ResolveBrush(Window owner, string key, Brush fallback)
    {
        return owner.TryFindResource(key) as Brush
            ?? Application.Current?.TryFindResource(key) as Brush
            ?? fallback;
    }
}

public sealed class TorrentFilesDialogR1644 : Window
{
    private readonly ObservableCollection<TorrentFileChoiceR1644> _files;
    public IReadOnlyList<TorrentFileChoiceR1644> Choices => _files;

    public TorrentFilesDialogR1644(Window owner, IEnumerable<TorrentFileChoiceR1644> files)
    {
        Owner = owner;
        Title = "Torrent Files - MediaDock";
        Width = 820;
        Height = 560;
        MinWidth = 660;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        TorrentDialogThemeR194.Apply(this, owner);
        _files = new ObservableCollection<TorrentFileChoiceR1644>(files);

        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Cancel", MinWidth = 92, Margin = new Thickness(0, 0, 8, 0) };
        var apply = new Button { Content = "Apply Priorities", MinWidth = 120, IsDefault = true };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        apply.Click += (_, _) => { DialogResult = true; Close(); };
        footer.Children.Add(cancel);
        footer.Children.Add(apply);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var grid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, ItemsSource = _files, SelectionMode = DataGridSelectionMode.Single };
        TorrentDialogThemeR194.ApplyGrid(grid, owner);
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Download", Binding = new System.Windows.Data.Binding(nameof(TorrentFileChoiceR1644.Selected)), Width = 78 });
        grid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "File", nameof(TorrentFileChoiceR1644.Path), new DataGridLength(1, DataGridLengthUnitType.Star)));
        grid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Size", nameof(TorrentFileChoiceR1644.SizeText), new DataGridLength(100), secondary: true));
        grid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Done", nameof(TorrentFileChoiceR1644.ProgressText), new DataGridLength(80), secondary: true));
        var priority = new DataGridComboBoxColumn { Header = "Priority", Width = 110, SelectedItemBinding = new System.Windows.Data.Binding(nameof(TorrentFileChoiceR1644.Priority)) };
        priority.ItemsSource = new[] { TorrentPriorityR1644.Lowest, TorrentPriorityR1644.Low, TorrentPriorityR1644.Normal, TorrentPriorityR1644.High, TorrentPriorityR1644.Highest, TorrentPriorityR1644.Immediate };
        grid.Columns.Add(priority);
        root.Children.Add(grid);
        Content = root;
    }
}

public sealed class TorrentDetailsDialogR1644 : Window
{
    public TorrentDetailsDialogR1644(
        Window owner,
        TorrentItemR1644 item,
        IReadOnlyList<TorrentFileChoiceR1644> files,
        IReadOnlyList<TorrentTrackerSnapshotR1644> trackers,
        IReadOnlyList<TorrentPeerSnapshotR1644> peers)
    {
        Owner = owner;
        Title = $"Torrent Details - {item.Name}";
        Width = 900;
        Height = 620;
        MinWidth = 720;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        TorrentDialogThemeR194.Apply(this, owner);

        var tabs = new TabControl();

        var generalText =
            $"Name: {item.Name}\n" +
            $"Status: {item.Status}\n" +
            $"Progress: {item.Progress:0.0}%\n" +
            $"Size: {item.SizeText}\n" +
            $"Downloaded: {item.Downloaded}\n" +
            $"Uploaded: {item.Uploaded}\n" +
            $"Download speed: {item.DownloadRate}\n" +
            $"Upload speed: {item.UploadRate}\n" +
            $"Peers: {item.Peers}\n" +
            $"Seeds: {item.Seeds}\n" +
            $"ETA: {item.Eta}\n" +
            $"Ratio: {item.Ratio}\n" +
            $"Peer discovery: {(string.IsNullOrWhiteSpace(item.DiscoveryStatus) ? "(waiting)" : item.DiscoveryStatus)}\n" +
            $"Tracker: {(string.IsNullOrWhiteSpace(item.TrackerStatus) ? "(waiting)" : item.TrackerStatus)}\n" +
            $"Last peer failure: {(string.IsNullOrWhiteSpace(item.PeerFailure) ? "(none)" : item.PeerFailure)}\n" +
            $"Save path: {item.SavePath}\n" +
            $"Source: {item.Source}\n" +
            $"Last error: {(string.IsNullOrWhiteSpace(item.LastError) ? "(none)" : item.LastError)}";
        tabs.Items.Add(new TabItem
        {
            Header = "General",
            Content = new ScrollViewer
            {
                Padding = new Thickness(14),
                Content = new TextBlock { Text = generalText, TextWrapping = TextWrapping.Wrap }
            }
        });

        var fileGrid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = true, ItemsSource = files };
        TorrentDialogThemeR194.ApplyGrid(fileGrid, owner);
        fileGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "File", nameof(TorrentFileChoiceR1644.Path), new DataGridLength(1, DataGridLengthUnitType.Star)));
        fileGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Size", nameof(TorrentFileChoiceR1644.SizeText), new DataGridLength(110), secondary: true));
        fileGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Done", nameof(TorrentFileChoiceR1644.ProgressText), new DataGridLength(90), secondary: true));
        fileGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Priority", nameof(TorrentFileChoiceR1644.Priority), new DataGridLength(110), secondary: true));
        tabs.Items.Add(new TabItem { Header = $"Files ({files.Count})", Content = fileGrid });

        var trackerGrid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = true, ItemsSource = trackers };
        TorrentDialogThemeR194.ApplyGrid(trackerGrid, owner);
        trackerGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Tracker", nameof(TorrentTrackerSnapshotR1644.Tracker), new DataGridLength(1, DataGridLengthUnitType.Star)));
        trackerGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Announce", nameof(TorrentTrackerSnapshotR1644.Announce), new DataGridLength(130), secondary: true));
        trackerGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Scrape", nameof(TorrentTrackerSnapshotR1644.Scrape), new DataGridLength(130), secondary: true));
        tabs.Items.Add(new TabItem { Header = $"Trackers ({trackers.Count})", Content = trackerGrid });

        var peerGrid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, IsReadOnly = true, ItemsSource = peers };
        TorrentDialogThemeR194.ApplyGrid(peerGrid, owner);
        peerGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Peer", nameof(TorrentPeerSnapshotR1644.Endpoint), new DataGridLength(1, DataGridLengthUnitType.Star)));
        peerGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Direction", nameof(TorrentPeerSnapshotR1644.Direction), new DataGridLength(100), secondary: true));
        peerGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Down", nameof(TorrentPeerSnapshotR1644.DownloadRate), new DataGridLength(100)));
        peerGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Up", nameof(TorrentPeerSnapshotR1644.UploadRate), new DataGridLength(100), secondary: true));
        peerGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Encryption", nameof(TorrentPeerSnapshotR1644.Encryption), new DataGridLength(120), secondary: true));
        peerGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(owner, "Client", nameof(TorrentPeerSnapshotR1644.Client), new DataGridLength(150), secondary: true));
        tabs.Items.Add(new TabItem { Header = $"Peers ({peers.Count})", Content = peerGrid });

        Content = tabs;
    }
}
