using System.Windows;

namespace MediaDownloader;

public enum ClipboardLinkChoiceR1639
{
    Ignore,
    Analyze
}

public partial class ClipboardLinkDetectedDialogR1639 : Window
{
    public ClipboardLinkChoiceR1639 Choice { get; private set; } = ClipboardLinkChoiceR1639.Ignore;

    public ClipboardLinkDetectedDialogR1639(string url)
    {
        InitializeComponent();
        DetectedUrlTextR1639.Text = url;
    }

    public static ClipboardLinkChoiceR1639 ShowFor(Window owner, string url)
    {
        var dialog = new ClipboardLinkDetectedDialogR1639(url)
        {
            Owner = owner
        };
        dialog.ShowDialog();
        return dialog.Choice;
    }

    private void Analyze_Click(object sender, RoutedEventArgs e)
    {
        Choice = ClipboardLinkChoiceR1639.Analyze;
        DialogResult = true;
    }

    private void Ignore_Click(object sender, RoutedEventArgs e)
    {
        Choice = ClipboardLinkChoiceR1639.Ignore;
        DialogResult = false;
    }
}
