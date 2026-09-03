using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace MediaDownloader;

internal sealed record DownloadFileDialogResultR1643(
    string FileName,
    string OutputDirectory,
    string Category,
    bool StartImmediately);

internal sealed class DownloadFileDialogR1643 : Window
{
    private readonly Window _themeOwner;
    private readonly TextBox _fileNameBox;
    private readonly TextBox _folderBox;
    private readonly ComboBox _categoryBox;
    private readonly CheckBox _rememberBox;
    private readonly string _baseDownloadDirectory;

    private DownloadFileDialogResultR1643? _result;

    private DownloadFileDialogR1643(
        Window owner,
        BrowserHandlerRequestR1643 request,
        string defaultDirectory)
    {
        _themeOwner = owner;
        _baseDownloadDirectory = Path.GetDirectoryName(defaultDirectory) ?? defaultDirectory;
        Owner = owner;
        Title = "Download File — MediaDock";
        var size = ThemeUiR1646.ResponsiveSize(owner, 0.56, 0.62, 640, 420, 840, 620);
        Width = size.Width;
        Height = size.Height;
        MinWidth = 620;
        MinHeight = 400;
        MaxWidth = Math.Max(MinWidth, SystemParameters.WorkArea.Width - 40);
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 60);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResizeWithGrip;
        ThemeUiR1646.ApplyWindow(this, owner);

        var suggestedName = Core.Services.GeneralDownloadClassifierR1643.ResolveSuggestedFileNameR1643(request);
        var category = DetectCategory(suggestedName, request.MimeType);
        var remembered = DownloadDialogPreferencesR1643.GetFolder(category);
        var initialFolder = Directory.Exists(remembered)
            ? remembered
            : Path.Combine(_baseDownloadDirectory, category);

        var root = new Grid
        {
            Margin = new Thickness(22),
            Background = ThemeUiR1646.ResolveBrush(owner, "ThemeWindowBackgroundBrush")
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
        header.Children.Add(new TextBlock
        {
            Text = "Download File",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = ThemeUiR1646.ResolveBrush(owner, "ThemePrimaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 4)
        });
        header.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(request.Source)
                ? "MediaDock will manage this download."
                : $"Captured from {request.Source}",
            Foreground = ThemeUiR1646.ResolveBrush(owner, "ThemeSecondaryTextBrush"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var urlBox = MakeReadOnlyValue(request.Url);
        AddLabeledRow(root, 1, "URL", urlBox);

        _categoryBox = new ComboBox
        {
            MinHeight = 34,
            Padding = new Thickness(8, 5, 8, 5),
            ItemsSource = new[] { "General", "Compressed", "Documents", "Programs", "Video", "Music", "Images", "Books", "AI Models" },
            SelectedItem = category
        };
        ThemeUiR1646.ApplyComboBox(_categoryBox, owner);
        _categoryBox.SelectionChanged += (_, _) => ApplyRememberedFolder();
        AddLabeledRow(root, 2, "Category", _categoryBox);

        var fileGrid = new Grid();
        fileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        fileGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _fileNameBox = MakeEditableValue(suggestedName);
        Grid.SetColumn(_fileNameBox, 0);
        fileGrid.Children.Add(_fileNameBox);
        var browseButton = MakeButton("Browse…", false, 92);
        browseButton.Margin = new Thickness(8, 0, 0, 0);
        browseButton.Click += BrowseSavePath;
        Grid.SetColumn(browseButton, 1);
        fileGrid.Children.Add(browseButton);
        AddLabeledRow(root, 3, "File name", fileGrid);

        _folderBox = MakeEditableValue(initialFolder);
        AddLabeledRow(root, 4, "Save to", _folderBox);

        var details = new StackPanel { Margin = new Thickness(112, 8, 0, 0) };
        _rememberBox = new CheckBox
        {
            Content = "Remember this folder for this category",
            IsChecked = true,
            Foreground = ThemeUiR1646.ResolveBrush(owner, "ThemePrimaryTextBrush"),
            Margin = new Thickness(0, 0, 0, 8)
        };
        details.Children.Add(_rememberBox);
        details.Children.Add(new TextBlock
        {
            Text = BuildDetailText(request),
            Foreground = ThemeUiR1646.ResolveBrush(owner, "ThemeMutedTextBrush"),
            FontSize = 10.5,
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(details, 5);
        root.Children.Add(details);

        var buttons = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };
        var later = MakeButton("Download Later", false, 112);
        later.Click += (_, _) => Complete(false);
        var start = MakeButton("Start Download", true, 112);
        start.Click += (_, _) => Complete(true);
        var cancel = MakeButton("Cancel", false, 88);
        cancel.Click += (_, _) => { DialogResult = false; Close(); };
        buttons.Children.Add(later);
        buttons.Children.Add(start);
        buttons.Children.Add(cancel);
        Grid.SetRow(buttons, 6);
        root.Children.Add(buttons);

        Content = root;
    }

    internal static DownloadFileDialogResultR1643? ShowFor(
        Window owner,
        BrowserHandlerRequestR1643 request,
        string defaultDirectory)
    {
        var dialog = new DownloadFileDialogR1643(owner, request, defaultDirectory);
        return dialog.ShowDialog() == true ? dialog._result : null;
    }

    private TextBox MakeReadOnlyValue(string value)
    {
        var box = new TextBox
        {
            Text = value,
            MinHeight = 34,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(9, 4, 9, 4)
        };
        ThemeUiR1646.ApplyTextBox(box, _themeOwner, readOnly: true);
        return box;
    }

    private TextBox MakeEditableValue(string value)
    {
        var box = new TextBox
        {
            Text = value,
            MinHeight = 34,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(9, 4, 9, 4)
        };
        ThemeUiR1646.ApplyTextBox(box, _themeOwner);
        return box;
    }

    private Button MakeButton(string text, bool primary, double minWidth = 100)
        => ThemeUiR1646.MakeButton(_themeOwner, text, primary, minWidth, 36);

    private void AddLabeledRow(Grid root, int row, string label, UIElement value)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ThemeUiR1646.ResolveBrush(_themeOwner, "ThemeSecondaryTextBrush"),
            FontSize = 11
        };
        grid.Children.Add(text);
        Grid.SetColumn(value, 1);
        grid.Children.Add(value);
        Grid.SetRow(grid, row);
        root.Children.Add(grid);
    }

    private void BrowseSavePath(object sender, RoutedEventArgs e)
    {
        var fileName = Core.Services.GeneralDownloadClassifierR1643.SanitizeFileNameR1643(_fileNameBox.Text);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "download.bin";
        }

        var dialog = new SaveFileDialog
        {
            FileName = fileName,
            InitialDirectory = Directory.Exists(_folderBox.Text) ? _folderBox.Text : string.Empty,
            Filter = "All files|*.*",
            AddExtension = false,
            OverwritePrompt = false,
            Title = "Choose where MediaDock should save this file"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _fileNameBox.Text = Path.GetFileName(dialog.FileName);
            _folderBox.Text = Path.GetDirectoryName(dialog.FileName) ?? _folderBox.Text;
        }
    }

    private void Complete(bool startImmediately)
    {
        var fileName = Core.Services.GeneralDownloadClassifierR1643.SanitizeFileNameR1643(_fileNameBox.Text);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            ThemedMessageBoxR1646.Show(this, "Enter a valid file name.", "MediaDock", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var folder = _folderBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            ThemedMessageBoxR1646.Show(this, "Choose a save folder.", "MediaDock", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            ThemedMessageBoxR1646.Show(this, ex.Message, "MediaDock — Save folder", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var category = _categoryBox.SelectedItem?.ToString() ?? "General";
        if (_rememberBox.IsChecked == true)
        {
            DownloadDialogPreferencesR1643.SetFolder(category, folder);
        }

        _result = new DownloadFileDialogResultR1643(fileName, folder, category, startImmediately);
        DialogResult = true;
        Close();
    }

    private void ApplyRememberedFolder()
    {
        var category = _categoryBox.SelectedItem?.ToString() ?? "General";
        var remembered = DownloadDialogPreferencesR1643.GetFolder(category);
        _folderBox.Text = Directory.Exists(remembered)
            ? remembered
            : Path.Combine(_baseDownloadDirectory, category);
    }

    private static string DetectCategory(string fileName, string? mimeType)
    {
        var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "7z", "ace", "arj", "bz2", "gz", "gzip", "lzh", "rar", "tar", "tgz", "xz", "zip", "zipx", "zst" }.Contains(ext)) return "Compressed";
        if (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "exe", "msi", "msu", "apk", "appx", "msix", "deb", "rpm", "pkg" }.Contains(ext)) return "Programs";
        if (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mp4", "mkv", "avi", "mov", "webm", "wmv", "m4v", "mpeg", "mpg", "ts" }.Contains(ext)) return "Video";
        if (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mp3", "m4a", "aac", "flac", "ogg", "opus", "wav", "wma" }.Contains(ext)) return "Music";
        if (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "jpg", "jpeg", "png", "gif", "webp", "tif", "tiff", "bmp", "heic" }.Contains(ext)) return "Images";
        if (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "epub", "mobi", "azw", "azw3", "pdf", "cbr", "cbz" }.Contains(ext)) return "Books";
        if (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gguf", "ggml", "safetensors", "onnx", "pt", "pth", "ckpt" }.Contains(ext)) return "AI Models";
        if ((mimeType ?? string.Empty).StartsWith("application/", StringComparison.OrdinalIgnoreCase) || new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "doc", "docx", "xls", "xlsx", "ppt", "pptx", "odt", "ods", "odp", "rtf", "txt" }.Contains(ext)) return "Documents";
        return "General";
    }

    private static string BuildDetailText(BrowserHandlerRequestR1643 request)
    {
        var size = request.ContentLength > 0 ? FormatBytes(request.ContentLength) : "Size will be detected when download starts";
        var type = string.IsNullOrWhiteSpace(request.MimeType) ? "Unknown type" : request.MimeType;
        return $"{size}    •    {type}";
    }

    private static string FormatBytes(long value)
    {
        var size = (double)value;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var index = 0;
        while (size >= 1024 && index < units.Length - 1)
        {
            size /= 1024;
            index++;
        }
        return index == 0 ? $"{size:0} {units[index]}" : $"{size:0.##} {units[index]}";
    }
}

internal static class DownloadDialogPreferencesR1643
{
    private static readonly string PathValue = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AJCoder", "MediaDock", "download-dialog-folders.json");

    internal static string GetFolder(string category)
    {
        try
        {
            if (!File.Exists(PathValue)) return string.Empty;
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(PathValue));
            return map is not null && map.TryGetValue(category, out var folder) ? folder : string.Empty;
        }
        catch { return string.Empty; }
    }

    internal static void SetFolder(string category, string folder)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(PathValue)!;
            Directory.CreateDirectory(directory);
            var map = File.Exists(PathValue)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(PathValue)) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            map[category] = folder;
            File.WriteAllText(PathValue, JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
