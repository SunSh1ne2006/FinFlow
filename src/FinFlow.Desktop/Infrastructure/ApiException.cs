namespace FinFlow.Desktop.Infrastructure;

/// <summary>Ошибка, возвращённая сервером приложений (ProblemDetails).</summary>
public class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
