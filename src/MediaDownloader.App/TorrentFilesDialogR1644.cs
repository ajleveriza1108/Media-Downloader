using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MediaDownloader.Core.Services;
using MonoTorrent;

namespace MediaDownloader;

public sealed class TorrentFilesDialogR1644 : Window
{
    private readonly ObservableCollection<TorrentFileChoiceR1644> _files;
    public IReadOnlyList<TorrentFileChoiceR1644> Choices => _files;

    public TorrentFilesDialogR1644(Window owner, IEnumerable<TorrentFileChoiceR1644> files)
    {
        Owner = owner;
        Title = "Torrent Files - MediaDock";
        Width = 760;
        Height = 520;
        MinWidth = 620;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _files = new ObservableCollection<TorrentFileChoiceR1644>(files);

        var root = new DockPanel { Margin = new Thickness(14) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Button { Content = "Cancel", MinWidth = 92, Margin = new Thickness(0, 0, 8, 0) };
        var apply = new Button { Content = "Apply Selection", MinWidth = 120, IsDefault = true };
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        apply.Click += (_, _) => { DialogResult = true; Close(); };
        footer.Children.Add(cancel);
        footer.Children.Add(apply);
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);

        var grid = new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, ItemsSource = _files, SelectionMode = DataGridSelectionMode.Single };
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Download", Binding = new System.Windows.Data.Binding(nameof(TorrentFileChoiceR1644.Selected)) });
        grid.Columns.Add(new DataGridTextColumn { Header = "File", Binding = new System.Windows.Data.Binding(nameof(TorrentFileChoiceR1644.Path)), Width = new DataGridLength(1, DataGridLengthUnitType.Star), IsReadOnly = true });
        grid.Columns.Add(new DataGridTextColumn { Header = "Size", Binding = new System.Windows.Data.Binding(nameof(TorrentFileChoiceR1644.SizeText)), Width = 100, IsReadOnly = true });
        var priority = new DataGridComboBoxColumn { Header = "Priority", Width = 110, SelectedItemBinding = new System.Windows.Data.Binding(nameof(TorrentFileChoiceR1644.Priority)) };
        priority.ItemsSource = new[] { Priority.Lowest, Priority.Low, Priority.Normal, Priority.High, Priority.Highest, Priority.Immediate };
        grid.Columns.Add(priority);
        root.Children.Add(grid);
        Content = root;
    }
}
