using System.Windows;
using MediaDownloader.ViewModels;

namespace MediaDownloader;

public partial class DiagnosticsWindow : Window
{
    public DiagnosticsWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
