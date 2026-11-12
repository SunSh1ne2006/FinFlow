using System.Windows;
using System.Windows.Controls;
using FinFlow.Desktop.ViewModels;

namespace FinFlow.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }

    private async void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        // Обновляем данные активной вкладки при переключении.
        if (!IsLoaded || e.Source is not TabControl || !ReferenceEquals(e.Source, sender))
        {
            return;
        }

        await _viewModel.RefreshTabAsync(_viewModel.SelectedTabIndex);
    }

    private void OnLogoutClick(object sender, RoutedEventArgs e)
    {
        var login = new LoginWindow();
        login.Show();
        Close();
    }
}
