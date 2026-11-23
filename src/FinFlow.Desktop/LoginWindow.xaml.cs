using System;
using System.Net.Http;
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
        LoginBox.Text = "employee1";
        ServerText.Text = "Сервер приложений: " + ApiSettings.BaseUrl;
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
                Password = PasswordBox.Password
            };

            var response = await _api.PostAsync<LoginResponse>("api/auth/login", request);
            if (response == null)
            {
                StatusText.Text = "Сервер вернул пустой ответ.";
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
            StatusText.Text = exception.StatusCode == 401
                ? "Неверный логин или пароль. Для демонстрационных учётных записей пароль — FinFlow#2026!"
                : exception.Message;
        }
        catch (HttpRequestException exception)
        {
            StatusText.Text = "Сервер приложений недоступен по адресу " + ApiSettings.BaseUrl
                              + ". Запустите проект FinFlow.Api и повторите вход. (" + exception.Message + ")";
        }
        catch (TaskCanceledException)
        {
            StatusText.Text = "Сервер приложений не ответил за отведённое время: " + ApiSettings.BaseUrl;
        }
        catch (Exception exception)
        {
            StatusText.Text = "Не удалось выполнить вход: " + exception.Message;
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }
}
