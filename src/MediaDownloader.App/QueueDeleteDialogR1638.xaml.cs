using System.Windows;
using System.Windows.Input;

namespace MediaDownloader;

public enum QueueDeleteChoiceR1638
{
    Cancel = 0,
    RemoveFromMediaDock = 1,
    DeleteFromComputer = 2
}

public partial class QueueDeleteDialogR1638 : Window
{
    public QueueDeleteChoiceR1638 Choice { get; private set; } = QueueDeleteChoiceR1638.Cancel;

    public QueueDeleteDialogR1638(string scopeText)
    {
        InitializeComponent();
        QuestionTextR1638.Text = $"Remove {scopeText}?";
    }

    public static QueueDeleteChoiceR1638 ShowFor(Window owner, string scopeText)
    {
        var dialog = new QueueDeleteDialogR1638(scopeText)
        {
            Owner = owner
        };
        _ = dialog.ShowDialog();
        return dialog.Choice;
    }

    private void DeleteFromComputerR1638_Click(object sender, RoutedEventArgs e)
    {
        Choice = QueueDeleteChoiceR1638.DeleteFromComputer;
        Close();
    }

    private void RemoveFromMediaDockR1638_Click(object sender, RoutedEventArgs e)
    {
        Choice = QueueDeleteChoiceR1638.RemoveFromMediaDock;
        Close();
    }

    private void CancelR1638_Click(object sender, RoutedEventArgs e)
    {
        Choice = QueueDeleteChoiceR1638.Cancel;
        Close();
    }

    private void QueueDeleteDialogR1638_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Choice = QueueDeleteChoiceR1638.Cancel;
        Close();
    }

    private void QueueDeleteDialogR1638_TitleMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
}
