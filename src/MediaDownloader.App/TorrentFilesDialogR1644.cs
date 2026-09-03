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
    public static void Apply(Window window, Window owner)
    {
        ThemeUiR1646.ApplyWindow(window, owner);
    }

    public static void ApplyGrid(DataGrid grid, Window owner)
    {
        var panel = ThemeUiR1646.ResolveBrush(owner, "ThemePanelBackgroundBrush");
        var surface = ThemeUiR1646.ResolveBrush(owner, "ThemeSurfaceBrush");
        var primary = ThemeUiR1646.ResolveBrush(owner, "ThemePrimaryTextBrush");
        var secondary = ThemeUiR1646.ResolveBrush(owner, "ThemeSecondaryTextBrush");
        var border = ThemeUiR1646.ResolveBrush(owner, "ThemeBorderBrush");
        var accentPanel = ThemeUiR1646.ResolveBrush(owner, "ThemeAccentPanelBrush");

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
        selected.Setters.Add(new Setter(Control.BackgroundProperty, accentPanel));
        selected.Setters.Add(new Setter(Control.ForegroundProperty, primary));
        rowStyle.Triggers.Add(selected);
        grid.RowStyle = rowStyle;

        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(Control.ForegroundProperty, primary));
        cellStyle.Setters.Add(new Setter(Control.BorderBrushProperty, border));
        cellStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        cellStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        cellStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        grid.CellStyle = cellStyle;
    }

    public static DataGridTextColumn CreateTextColumn(
        Window owner,
        string header,
        string propertyName,
        DataGridLength width,
        bool secondary = false)
    {
        var foreground = ThemeUiR1646.ResolveBrush(
            owner,
            secondary ? "ThemeSecondaryTextBrush" : "ThemePrimaryTextBrush");
        var elementStyle = new Style(typeof(TextBlock));
        elementStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, foreground));
        elementStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, owner.FontFamily));
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
        ThemeUiR1646.ApplyStyle(button, owner, styleKey);
    }

    public static void ApplyTabs(TabControl tabs, Window owner)
    {
        var panel = ThemeUiR1646.ResolveBrush(owner, "ThemePanelBackgroundBrush");
        var surface = ThemeUiR1646.ResolveBrush(owner, "ThemeSurfaceBrush");
        var primary = ThemeUiR1646.ResolveBrush(owner, "ThemePrimaryTextBrush");
        var secondary = ThemeUiR1646.ResolveBrush(owner, "ThemeSecondaryTextBrush");
        var border = ThemeUiR1646.ResolveBrush(owner, "ThemeBorderBrush");
        var selected = ThemeUiR1646.ResolveBrush(owner, "ThemeAccentPanelBrush");
        tabs.Background = panel;
        tabs.Foreground = primary;
        tabs.BorderBrush = border;

        var itemStyle = new Style(typeof(TabItem));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, secondary));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, surface));
        itemStyle.Setters.Add(new Setter(Control.BorderBrushProperty, border));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 7, 12, 7)));
        var trigger = new Trigger { Property = TabItem.IsSelectedProperty, Value = true };
        trigger.Setters.Add(new Setter(Control.BackgroundProperty, selected));
        trigger.Setters.Add(new Setter(Control.ForegroundProperty, primary));
        trigger.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        itemStyle.Triggers.Add(trigger);
        tabs.ItemContainerStyle = itemStyle;
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
        var size = ThemeUiR1646.ResponsiveSize(owner, 0.64, 0.70, 660, 440, 980, 720);
        Width = size.Width;
        Height = size.Height;
        MinWidth = 640;
        MinHeight = 420;
        MaxWidth = Math.Max(MinWidth, SystemParameters.WorkArea.Width - 40);
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 60);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        TorrentDialogThemeR194.Apply(this, owner);
        _files = new ObservableCollection<TorrentFileChoiceR1644>(files);

        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = ThemeUiR1646.MakeButton(owner, "Cancel", false, 92, 36);
        var apply = ThemeUiR1646.MakeButton(owner, "Apply Priorities", true, 120, 36);
        apply.IsDefault = true;
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
        var size = ThemeUiR1646.ResponsiveSize(owner, 0.70, 0.76, 720, 500, 1060, 780);
        Width = size.Width;
        Height = size.Height;
        MinWidth = 700;
        MinHeight = 480;
        MaxWidth = Math.Max(MinWidth, SystemParameters.WorkArea.Width - 40);
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 60);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        TorrentDialogThemeR194.Apply(this, owner);

        var tabs = new TabControl();
        TorrentDialogThemeR194.ApplyTabs(tabs, owner);

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
                Content = new TextBlock { Text = generalText, TextWrapping = TextWrapping.Wrap, Foreground = ThemeUiR1646.ResolveBrush(owner, "ThemePrimaryTextBrush") }
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
