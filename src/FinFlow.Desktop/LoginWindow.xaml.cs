using System;
using System.Threading.Tasks;
using System.Windows;
using FinFlow.Contracts;
using FinFlow.Desktop.Infrastructure;
using FinFlow.Desktop.ViewModels;

namespace FinFlow.Desktop;

public partial class LoginWindow : Window
{
    private readonly ApiClient _api = new(ApiSettings.BaseUrl);

    public LoginWindow()
    {
        InitializeComponent();
        LoginBox.Text = "operator1";
        LoginBox.Focus();
    }

    private async void OnLoginClick(object sender, RoutedEventArgs e)
    {
        LoginButton.IsEnabled = false;
        StatusText.Text = null;
        try
        {
            var request = new LoginRequest
            {
                Login = LoginBox.Text.Trim(),
                Password = PasswordBox.Password,
                SecondFactor = string.IsNullOrWhiteSpace(SecondFactorBox.Text) ? null : SecondFactorBox.Text.Trim()
            };

            var response = await _api.PostAsync<LoginResponse>("api/auth/login", request);
            if (response == null)
            {
                StatusText.Text = "Сервер вернул пустой ответ.";
                return;
            }

            if (response.SecondFactorRequired)
            {
                StatusText.Text = "Требуется второй фактор: введите код и повторите вход.";
                return;
            }

            if (string.IsNullOrWhiteSpace(response.Token))
            {
                StatusText.Text = "Не удалось получить токен доступа.";
                return;
            }

            _api.SetToken(response.Token);
            var session = new Session(_api, response.User);
            var window = new MainWindow(new MainViewModel(session));
            window.Show();
            Close();
        }
        catch (ApiException exception)
        {
            StatusText.Text = exception.Message;
        }
        catch (Exception exception)
        {
            StatusText.Text = "Сервер недоступен: " + exception.Message;
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }
}
