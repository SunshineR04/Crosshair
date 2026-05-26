using System.ComponentModel;
using System.Windows;
using CrosshairOverlay.ViewModels;

namespace CrosshairOverlay;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.CloseRequested += HideToTray;
    }

    private void HideToTray()
    {
        _viewModel.SaveSettings();
        Hide();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _viewModel.SaveSettings();
        e.Cancel = true;
        Hide();
    }
}
