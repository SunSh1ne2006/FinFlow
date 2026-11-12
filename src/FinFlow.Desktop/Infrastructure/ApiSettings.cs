namespace FinFlow.Desktop.Infrastructure;

/// <summary>Адрес сервера приложений. Может быть переопределён переменной окружения FINFLOW_API.</summary>
public static class ApiSettings
{
    public static string BaseUrl
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("FINFLOW_API");
            return string.IsNullOrWhiteSpace(value) ? "http://localhost:5080" : value.TrimEnd('/');
        }
    }
}
