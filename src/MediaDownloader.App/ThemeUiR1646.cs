using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace MediaDownloader;

// MEDIADOCK_THEME_ADAPTIVE_UI_R1646
internal static class ThemeUiR1646
{
    private static readonly Brush FallbackWindow = new SolidColorBrush(Color.FromRgb(246, 249, 253));
    private static readonly Brush FallbackPanel = Brushes.White;
    private static readonly Brush FallbackSurface = new SolidColorBrush(Color.FromRgb(240, 245, 251));
    private static readonly Brush FallbackInput = Brushes.White;
    private static readonly Brush FallbackPrimary = new SolidColorBrush(Color.FromRgb(20, 31, 45));
    private static readonly Brush FallbackSecondary = new SolidColorBrush(Color.FromRgb(79, 96, 118));
    private static readonly Brush FallbackMuted = new SolidColorBrush(Color.FromRgb(108, 126, 149));
    private static readonly Brush FallbackBorder = new SolidColorBrush(Color.FromRgb(199, 212, 229));
    private static readonly Brush FallbackStrongBorder = new SolidColorBrush(Color.FromRgb(161, 181, 207));
    private static readonly Brush FallbackAccent = new SolidColorBrush(Color.FromRgb(28, 125, 240));
    private static readonly Brush FallbackAccentPanel = new SolidColorBrush(Color.FromRgb(226, 239, 255));
    private static readonly Brush FallbackHover = new SolidColorBrush(Color.FromRgb(233, 240, 249));
    private static readonly Brush FallbackDanger = new SolidColorBrush(Color.FromRgb(196, 43, 28));
    private static readonly Brush FallbackSuccess = new SolidColorBrush(Color.FromRgb(21, 133, 86));

    internal static Brush ResolveBrush(FrameworkElement? scope, string key)
    {
        return scope?.TryFindResource(key) as Brush
            ?? Application.Current?.TryFindResource(key) as Brush
            ?? key switch
            {
                "ThemeWindowBackgroundBrush" => FallbackWindow,
                "ThemePanelBackgroundBrush" => FallbackPanel,
                "ThemeSurfaceBrush" => FallbackSurface,
                "ThemeInputBackgroundBrush" => FallbackInput,
                "ThemePrimaryTextBrush" => FallbackPrimary,
                "ThemeSecondaryTextBrush" => FallbackSecondary,
                "ThemeMutedTextBrush" => FallbackMuted,
                "ThemeBorderBrush" => FallbackBorder,
                "ThemeStrongBorderBrush" => FallbackStrongBorder,
                "ThemeAccentBrush" => FallbackAccent,
                "ThemeAccentPanelBrush" => FallbackAccentPanel,
                "ThemeHoverBrush" => FallbackHover,
                "ThemeDangerBrush" => FallbackDanger,
                "ThemeSuccessBrush" => FallbackSuccess,
                _ => FallbackPrimary
            };
    }

    internal static void ApplyWindow(Window window, Window? owner)
    {
        window.FontFamily = owner?.FontFamily ?? new FontFamily("Segoe UI");
        window.FontSize = owner is not null && owner.FontSize > 0 ? owner.FontSize : 12d;
        window.Background = ResolveBrush(owner ?? window, "ThemeWindowBackgroundBrush");
        window.Foreground = ResolveBrush(owner ?? window, "ThemePrimaryTextBrush");
        window.UseLayoutRounding = true;
        window.SnapsToDevicePixels = true;

        void ApplyCaption() => ApplyNativeTitleBar(window, ResolveBrush(owner ?? window, "ThemeWindowBackgroundBrush"));
        window.SourceInitialized += (_, _) => ApplyCaption();
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
            ApplyCaption();
    }

    private static void ApplyNativeTitleBar(Window window, Brush background)
    {
        if (window.WindowStyle == WindowStyle.None)
            return;

        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return;
            var enabled = IsDark(background) ? 1 : 0;
            if (DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int)) != 0)
                _ = DwmSetWindowAttribute(hwnd, 19, ref enabled, sizeof(int));
        }
        catch
        {
            // Theme title-bar integration is cosmetic and must not block a dialog.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    internal static void ApplyStyle(Control control, FrameworkElement? owner, string styleKey)
    {
        if (owner?.TryFindResource(styleKey) is Style ownerStyle)
        {
            control.Style = ownerStyle;
            return;
        }

        if (Application.Current?.MainWindow?.TryFindResource(styleKey) is Style mainWindowStyle)
        {
            control.Style = mainWindowStyle;
            return;
        }

        if (Application.Current?.TryFindResource(styleKey) is Style appStyle)
        {
            control.Style = appStyle;
        }
    }

    internal static void ApplyTextBox(TextBox box, FrameworkElement? owner, bool readOnly = false)
    {
        ApplyStyle(box, owner, "DarkTextBox");
        if (box.Style is null)
        {
            box.Background = ResolveBrush(owner, "ThemeInputBackgroundBrush");
            box.Foreground = ResolveBrush(owner, readOnly ? "ThemeSecondaryTextBrush" : "ThemePrimaryTextBrush");
            box.BorderBrush = ResolveBrush(owner, "ThemeBorderBrush");
            box.CaretBrush = ResolveBrush(owner, "ThemeAccentBrush");
            box.BorderThickness = new Thickness(1);
        }
        box.IsReadOnly = readOnly;
    }

    internal static void ApplyComboBox(ComboBox box, FrameworkElement? owner)
    {
        ApplyStyle(box, owner, "DarkComboBox");
        if (box.Style is null)
        {
            box.Background = ResolveBrush(owner, "ThemeInputBackgroundBrush");
            box.Foreground = ResolveBrush(owner, "ThemePrimaryTextBrush");
            box.BorderBrush = ResolveBrush(owner, "ThemeBorderBrush");
            box.BorderThickness = new Thickness(1);
        }
    }

    internal static Button MakeButton(
        FrameworkElement? owner,
        string text,
        bool primary = false,
        double minWidth = 96,
        double minHeight = 36)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = minWidth,
            MinHeight = minHeight,
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(6, 0, 0, 0),
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal
        };

        ApplyStyle(button, owner, primary ? "NeonPrimaryButton" : "DenseGhostButton");
        if (button.Style is null)
        {
            button.Background = ResolveBrush(owner, primary ? "ThemeAccentBrush" : "ThemeSurfaceBrush");
            button.Foreground = ResolveBrush(owner, primary ? "ThemeWindowBackgroundBrush" : "ThemePrimaryTextBrush");
            button.BorderBrush = ResolveBrush(owner, primary ? "ThemeAccentBrush" : "ThemeBorderBrush");
            button.BorderThickness = new Thickness(1);
        }

        return button;
    }

    internal static (double Width, double Height) ResponsiveSize(
        Window? owner,
        double widthRatio,
        double heightRatio,
        double minWidth,
        double minHeight,
        double maxWidth,
        double maxHeight)
    {
        var work = SystemParameters.WorkArea;
        var ownerWidth = owner is not null && owner.ActualWidth > 0 ? owner.ActualWidth : work.Width;
        var ownerHeight = owner is not null && owner.ActualHeight > 0 ? owner.ActualHeight : work.Height;
        var hardMaxWidth = Math.Max(minWidth, Math.Min(maxWidth, work.Width - 48));
        var hardMaxHeight = Math.Max(minHeight, Math.Min(maxHeight, work.Height - 64));
        return (
            Math.Clamp(ownerWidth * widthRatio, minWidth, hardMaxWidth),
            Math.Clamp(ownerHeight * heightRatio, minHeight, hardMaxHeight));
    }

    internal static bool IsDark(Brush brush)
    {
        if (brush is SolidColorBrush solid)
        {
            var c = solid.Color;
            var luminance = (0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B);
            return luminance < 128;
        }
        return false;
    }
}

internal static class ThemedMessageBoxR1646
{
    internal static MessageBoxResult Show(string messageBoxText)
        => ShowCore(Application.Current?.MainWindow, messageBoxText, "MediaDock", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.None);

    internal static MessageBoxResult Show(string messageBoxText, string caption)
        => ShowCore(Application.Current?.MainWindow, messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.None);

    internal static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button)
        => ShowCore(Application.Current?.MainWindow, messageBoxText, caption, button, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.None);

    internal static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        => ShowCore(Application.Current?.MainWindow, messageBoxText, caption, button, icon, MessageBoxResult.None, MessageBoxOptions.None);

    internal static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        => ShowCore(Application.Current?.MainWindow, messageBoxText, caption, button, icon, defaultResult, MessageBoxOptions.None);

    internal static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options)
        => ShowCore(Application.Current?.MainWindow, messageBoxText, caption, button, icon, defaultResult, options);

    internal static MessageBoxResult Show(Window owner, string messageBoxText)
        => ShowCore(owner, messageBoxText, "MediaDock", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.None);

    internal static MessageBoxResult Show(Window owner, string messageBoxText, string caption)
        => ShowCore(owner, messageBoxText, caption, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.None);

    internal static MessageBoxResult Show(Window owner, string messageBoxText, string caption, MessageBoxButton button)
        => ShowCore(owner, messageBoxText, caption, button, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.None);

    internal static MessageBoxResult Show(Window owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon)
        => ShowCore(owner, messageBoxText, caption, button, icon, MessageBoxResult.None, MessageBoxOptions.None);

    internal static MessageBoxResult Show(Window owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
        => ShowCore(owner, messageBoxText, caption, button, icon, defaultResult, MessageBoxOptions.None);

    internal static MessageBoxResult Show(Window owner, string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult, MessageBoxOptions options)
        => ShowCore(owner, messageBoxText, caption, button, icon, defaultResult, options);

    internal static void RunSelfTestR1646()
    {
        if (BuildButtonResults(MessageBoxButton.OK).Count != 1 ||
            BuildButtonResults(MessageBoxButton.YesNo).Count != 2 ||
            BuildButtonResults(MessageBoxButton.YesNoCancel).Count != 3 ||
            GetCloseResult(MessageBoxButton.YesNo) != MessageBoxResult.No ||
            GetCloseResult(MessageBoxButton.YesNoCancel) != MessageBoxResult.Cancel)
        {
            throw new InvalidOperationException("R1.6.53 themed dialog result contract failed.");
        }
    }

    private static MessageBoxResult ShowCore(
        Window? owner,
        string messageBoxText,
        string caption,
        MessageBoxButton buttons,
        MessageBoxImage icon,
        MessageBoxResult defaultResult,
        MessageBoxOptions options)
    {
        owner ??= Application.Current?.MainWindow;
        var dialog = new Window
        {
            Owner = owner,
            Title = string.IsNullOrWhiteSpace(caption) ? "MediaDock" : caption,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.Height,
            MinWidth = 420,
            MaxWidth = 680,
            MaxHeight = Math.Max(360, SystemParameters.WorkArea.Height * 0.82)
        };
        ThemeUiR1646.ApplyWindow(dialog, owner);

        var ownerWidth = owner is not null && owner.ActualWidth > 0 ? owner.ActualWidth : SystemParameters.WorkArea.Width;
        dialog.Width = Math.Clamp(ownerWidth * 0.42, 480, 640);
        dialog.FlowDirection = (options & MessageBoxOptions.RtlReading) != 0 ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        var root = new Border
        {
            Background = ThemeUiR1646.ResolveBrush(owner ?? dialog, "ThemeWindowBackgroundBrush"),
            BorderBrush = ThemeUiR1646.ResolveBrush(owner ?? dialog, "ThemeStrongBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10)
        };
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleBar = new Border
        {
            Background = ThemeUiR1646.ResolveBrush(owner ?? dialog, "ThemePanelBackgroundBrush"),
            BorderBrush = ThemeUiR1646.ResolveBrush(owner ?? dialog, "ThemeBorderBrush"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 9, 8, 9)
        };
        var titleGrid = new Grid();
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock
        {
            Text = dialog.Title,
            Foreground = ThemeUiR1646.ResolveBrush(owner ?? dialog, "ThemePrimaryTextBrush"),
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        titleGrid.Children.Add(title);
        var close = new Button
        {
            Content = "×",
            Width = 30,
            Height = 28,
            Padding = new Thickness(0),
            Margin = new Thickness(8, 0, 0, 0),
            ToolTip = "Close"
        };
        ThemeUiR1646.ApplyStyle(close, owner, "TitleBarCloseButton");
        if (close.Style is null)
        {
            close.Background = Brushes.Transparent;
            close.Foreground = ThemeUiR1646.ResolveBrush(owner ?? dialog, "ThemeSecondaryTextBrush");
            close.BorderThickness = new Thickness(0);
        }
        Grid.SetColumn(close, 1);
        titleGrid.Children.Add(close);
        titleBar.Child = titleGrid;
        titleBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try { dialog.DragMove(); } catch { }
            }
        };
        layout.Children.Add(titleBar);

        var contentScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(22, 20, 22, 18)
        };
        var contentGrid = new Grid();
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var iconBorder = new Border
        {
            Width = 38,
            Height = 38,
            Margin = new Thickness(0, 1, 16, 0),
            CornerRadius = new CornerRadius(19),
            Background = ThemeUiR1646.ResolveBrush(owner ?? dialog, "ThemeAccentPanelBrush"),
            BorderThickness = new Thickness(1)
        };
        var iconText = new TextBlock
        {
            Text = IconGlyph(icon),
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var iconBrushKey = icon switch
        {
            MessageBoxImage.Error => "ThemeDangerBrush",
            MessageBoxImage.Warning => "ThemeDangerBrush",
            _ => "ThemeAccentBrush"
        };
        var iconBrush = ThemeUiR1646.ResolveBrush(owner ?? dialog, iconBrushKey);
        iconBorder.BorderBrush = iconBrush;
        iconText.Foreground = iconBrush;
        iconBorder.Child = iconText;
        contentGrid.Children.Add(iconBorder);

        var message = new TextBlock
        {
            Text = messageBoxText ?? string.Empty,
            Foreground = ThemeUiR1646.ResolveBrush(owner ?? dialog, "ThemePrimaryTextBrush"),
            FontSize = 12.5,
            LineHeight = 19,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = (options & MessageBoxOptions.RightAlign) != 0 ? TextAlignment.Right : TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(message, 1);
        contentGrid.Children.Add(message);
        contentScroll.Content = contentGrid;
        Grid.SetRow(contentScroll, 1);
        layout.Children.Add(contentScroll);

        var footer = new Border
        {
            Background = ThemeUiR1646.ResolveBrush(owner ?? dialog, "ThemePanelBackgroundBrush"),
            BorderBrush = ThemeUiR1646.ResolveBrush(owner ?? dialog, "ThemeBorderBrush"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 12, 16, 12)
        };
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var buttonPanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(buttonPanel, 1);
        footerGrid.Children.Add(buttonPanel);
        footer.Child = footerGrid;
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);

        var selected = MessageBoxResult.None;
        var results = BuildButtonResults(buttons);
        foreach (var result in results)
        {
            var primary = result == GetPrimaryResult(buttons);
            var button = ThemeUiR1646.MakeButton(owner, ButtonText(result), primary, 88, 34);
            button.IsDefault = defaultResult != MessageBoxResult.None ? result == defaultResult : primary;
            button.IsCancel = result == MessageBoxResult.Cancel;
            button.Click += (_, _) =>
            {
                selected = result;
                dialog.Close();
            };
            buttonPanel.Children.Add(button);
        }

        close.Click += (_, _) =>
        {
            selected = GetCloseResult(buttons);
            dialog.Close();
        };
        dialog.Closing += (_, _) =>
        {
            if (selected == MessageBoxResult.None)
                selected = GetCloseResult(buttons);
        };
        dialog.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                selected = GetCloseResult(buttons);
                dialog.Close();
                e.Handled = true;
            }
        };

        root.Child = layout;
        dialog.Content = root;
        _ = dialog.ShowDialog();
        return selected == MessageBoxResult.None ? GetCloseResult(buttons) : selected;
    }

    private static List<MessageBoxResult> BuildButtonResults(MessageBoxButton buttons) => buttons switch
    {
        MessageBoxButton.OK => new() { MessageBoxResult.OK },
        MessageBoxButton.OKCancel => new() { MessageBoxResult.Cancel, MessageBoxResult.OK },
        MessageBoxButton.YesNo => new() { MessageBoxResult.No, MessageBoxResult.Yes },
        MessageBoxButton.YesNoCancel => new() { MessageBoxResult.Cancel, MessageBoxResult.No, MessageBoxResult.Yes },
        _ => new() { MessageBoxResult.OK }
    };

    private static MessageBoxResult GetPrimaryResult(MessageBoxButton buttons) => buttons switch
    {
        MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel => MessageBoxResult.Yes,
        _ => MessageBoxResult.OK
    };

    private static MessageBoxResult GetCloseResult(MessageBoxButton buttons) => buttons switch
    {
        MessageBoxButton.OK => MessageBoxResult.OK,
        MessageBoxButton.YesNo => MessageBoxResult.No,
        _ => MessageBoxResult.Cancel
    };

    private static string ButtonText(MessageBoxResult result) => result switch
    {
        MessageBoxResult.Yes => "Yes",
        MessageBoxResult.No => "No",
        MessageBoxResult.Cancel => "Cancel",
        _ => "OK"
    };

    private static string IconGlyph(MessageBoxImage icon) => icon switch
    {
        MessageBoxImage.Error => "×",
        MessageBoxImage.Warning => "!",
        MessageBoxImage.Question => "?",
        MessageBoxImage.Information => "i",
        _ => "i"
    };
}
