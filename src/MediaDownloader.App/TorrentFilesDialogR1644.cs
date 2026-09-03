using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaDownloader.Core.Services;
using Microsoft.Win32;

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

// MEDIADOCK_ADD_TORRENT_DIALOG_R1651
// Theme-matched pre-add dialog. A .torrent is only committed to TorrentHost after
// the user chooses the destination, file selection, containing-folder behavior,
// and whether the transfer should start immediately.
public sealed class TorrentAddDialogR1651 : Window
{
    private readonly TorrentPreviewR1644 _preview;
    private readonly TextBox _savePath;
    private readonly CheckBox _createSubfolder;
    private readonly CheckBox _startTorrent;
    private readonly TextBlock _selectionSummary;

    public string SavePath => _savePath.Text.Trim();
    public bool CreateSubfolder => _createSubfolder.IsChecked == true;
    public bool StartImmediately => _startTorrent.IsChecked == true;

    public TorrentAddDialogR1651(
        Window owner,
        TorrentPreviewR1644 preview,
        string defaultSavePath,
        bool defaultStartImmediately)
    {
        _preview = preview;
        Owner = owner;
        Title = $"Add New Torrent - {preview.Name}";
        // MEDIADOCK_ADD_TORRENT_CONTENT_SPACE_R1653
        // Give the file selector the majority of the dialog on normal laptop displays.
        // The window remains resizable and bounded to the usable work area.
        var size = ThemeUiR1646.ResponsiveSize(owner, 0.84, 0.90, 820, 650, 1280, 940);
        Width = size.Width;
        Height = size.Height;
        MinWidth = 780;
        MinHeight = 620;
        MaxWidth = Math.Max(MinWidth, SystemParameters.WorkArea.Width - 24);
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 20);
        ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        TorrentDialogThemeR194.Apply(this, owner);

        var primary = ThemeUiR1646.ResolveBrush(owner, "ThemePrimaryTextBrush");
        var secondary = ThemeUiR1646.ResolveBrush(owner, "ThemeSecondaryTextBrush");
        var border = ThemeUiR1646.ResolveBrush(owner, "ThemeBorderBrush");
        var panel = ThemeUiR1646.ResolveBrush(owner, "ThemePanelBackgroundBrush");
        var surface = ThemeUiR1646.ResolveBrush(owner, "ThemeSurfaceBrush");

        var root = new Grid { Margin = new Thickness(16) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        heading.Children.Add(new TextBlock
        {
            Text = "Add New Torrent",
            Foreground = primary,
            FontSize = 21,
            FontWeight = FontWeights.SemiBold
        });
        heading.Children.Add(new TextBlock
        {
            Text = preview.Name,
            Foreground = secondary,
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        heading.Children.Add(new TextBlock
        {
            Text = $"{preview.TotalSizeText} • {(preview.IsMagnet ? "Magnet metadata" : ".torrent metadata loaded")}",
            Foreground = secondary,
            Margin = new Thickness(0, 3, 0, 0)
        });
        Grid.SetRow(heading, 0);
        root.Children.Add(heading);

        var saveBorder = new Border
        {
            Background = panel,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 10)
        };
        var saveGrid = new Grid();
        saveGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        saveGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        saveGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        saveGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var saveLabel = new TextBlock
        {
            Text = "Save in",
            Foreground = primary,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        };
        Grid.SetColumnSpan(saveLabel, 2);
        saveGrid.Children.Add(saveLabel);

        _savePath = new TextBox
        {
            Text = defaultSavePath,
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        ThemeUiR1646.ApplyTextBox(_savePath, owner);
        Grid.SetRow(_savePath, 1);
        saveGrid.Children.Add(_savePath);

        var browse = ThemeUiR1646.MakeButton(owner, "Browse…", false, 96, 36);
        browse.Click += (_, _) =>
        {
            var folderDialog = new OpenFolderDialog
            {
                Title = "Choose where MediaDock should save this torrent",
                Multiselect = false,
                InitialDirectory = Directory.Exists(SavePath) ? SavePath : defaultSavePath
            };
            if (folderDialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(folderDialog.FolderName))
            {
                _savePath.Text = folderDialog.FolderName;
            }
        };
        Grid.SetRow(browse, 1);
        Grid.SetColumn(browse, 1);
        saveGrid.Children.Add(browse);
        saveBorder.Child = saveGrid;
        Grid.SetRow(saveBorder, 1);
        root.Children.Add(saveBorder);

        var optionPanel = new WrapPanel { Margin = new Thickness(2, 0, 0, 10) };
        _createSubfolder = new CheckBox
        {
            Content = "Create containing subfolder",
            IsChecked = true,
            Foreground = primary,
            Margin = new Thickness(0, 0, 24, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        _startTorrent = new CheckBox
        {
            Content = "Start torrent immediately",
            IsChecked = defaultStartImmediately,
            Foreground = primary,
            VerticalAlignment = VerticalAlignment.Center
        };
        optionPanel.Children.Add(_createSubfolder);
        optionPanel.Children.Add(_startTorrent);
        Grid.SetRow(optionPanel, 2);
        root.Children.Add(optionPanel);

        var contentsBorder = new Border
        {
            Background = surface,
            BorderBrush = border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            MinHeight = 340
        };
        var contents = new DockPanel();

        var contentsHeader = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        contentsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        contentsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerText = new StackPanel();
        headerText.Children.Add(new TextBlock
        {
            Text = "Torrent contents",
            Foreground = primary,
            FontWeight = FontWeights.SemiBold
        });
        _selectionSummary = new TextBlock
        {
            Foreground = secondary,
            Margin = new Thickness(0, 2, 0, 0)
        };
        headerText.Children.Add(_selectionSummary);
        contentsHeader.Children.Add(headerText);

        var selectionButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var selectAll = ThemeUiR1646.MakeButton(owner, "Select All", false, 92, 32);
        var selectNone = ThemeUiR1646.MakeButton(owner, "Select None", false, 96, 32);
        selectAll.Click += (_, _) =>
        {
            foreach (var file in _preview.Files) file.Selected = true;
            UpdateSelectionSummaryR1651();
        };
        selectNone.Click += (_, _) =>
        {
            foreach (var file in _preview.Files) file.Selected = false;
            UpdateSelectionSummaryR1651();
        };
        selectionButtons.Children.Add(selectAll);
        selectionButtons.Children.Add(selectNone);
        Grid.SetColumn(selectionButtons, 1);
        contentsHeader.Children.Add(selectionButtons);
        DockPanel.SetDock(contentsHeader, Dock.Top);
        contents.Children.Add(contentsHeader);

        if (preview.Files.Count > 0)
        {
            foreach (var file in preview.Files)
            {
                file.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(TorrentPreviewFileR1644.Selected))
                    {
                        UpdateSelectionSummaryR1651();
                    }
                };
            }

            var fileGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                ItemsSource = preview.Files,
                SelectionMode = DataGridSelectionMode.Single,
                MinHeight = 300,
                RowHeight = 34,
                ColumnHeaderHeight = 34
            };
            TorrentDialogThemeR194.ApplyGrid(fileGrid, owner);
            fileGrid.Columns.Add(new DataGridCheckBoxColumn
            {
                Header = "Download",
                Binding = new System.Windows.Data.Binding(nameof(TorrentPreviewFileR1644.Selected))
                {
                    Mode = System.Windows.Data.BindingMode.TwoWay,
                    UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
                },
                Width = 84
            });
            fileGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(
                owner, "Name", nameof(TorrentPreviewFileR1644.Path),
                new DataGridLength(1, DataGridLengthUnitType.Star)));
            fileGrid.Columns.Add(TorrentDialogThemeR194.CreateTextColumn(
                owner, "Size", nameof(TorrentPreviewFileR1644.SizeText),
                new DataGridLength(110), secondary: true));
            contents.Children.Add(fileGrid);
        }
        else
        {
            selectAll.IsEnabled = false;
            selectNone.IsEnabled = false;
            contents.Children.Add(new TextBlock
            {
                Text = "File list will become available after magnet metadata is received. All files will be enabled initially.",
                Foreground = secondary,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 12, 4, 12)
            });
        }

        contentsBorder.Child = contents;
        Grid.SetRow(contentsBorder, 3);
        root.Children.Add(contentsBorder);

        var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var hint = new TextBlock
        {
            Text = "Nothing is added until you choose Add Torrent.",
            Foreground = secondary,
            VerticalAlignment = VerticalAlignment.Center
        };
        footer.Children.Add(hint);

        var footerButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancel = ThemeUiR1646.MakeButton(owner, "Cancel", false, 92, 36);
        var add = ThemeUiR1646.MakeButton(owner, "Add Torrent", true, 112, 36);
        add.IsDefault = true;
        cancel.IsCancel = true;
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        add.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(SavePath))
            {
                ThemedMessageBoxR1646.Show(this, "Choose a save folder first.", "MediaDock Torrent",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _ = Path.GetFullPath(SavePath);
            }
            catch
            {
                ThemedMessageBoxR1646.Show(this, "The selected save folder is not valid.", "MediaDock Torrent",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_preview.Files.Count > 0 && !_preview.Files.Any(file => file.Selected))
            {
                ThemedMessageBoxR1646.Show(this, "Select at least one file to download.", "MediaDock Torrent",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
            Close();
        };
        footerButtons.Children.Add(cancel);
        footerButtons.Children.Add(add);
        Grid.SetColumn(footerButtons, 1);
        footer.Children.Add(footerButtons);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
        UpdateSelectionSummaryR1651();
    }

    private void UpdateSelectionSummaryR1651()
    {
        if (_preview.Files.Count == 0)
        {
            _selectionSummary.Text = "Metadata pending";
            return;
        }

        var selected = _preview.Files.Where(file => file.Selected).ToArray();
        var selectedBytes = selected.Sum(file => file.Length);
        _selectionSummary.Text =
            $"{selected.Length} of {_preview.Files.Count} files selected • {TorrentClientR1644.FormatSizeR1644(selectedBytes)}";
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
