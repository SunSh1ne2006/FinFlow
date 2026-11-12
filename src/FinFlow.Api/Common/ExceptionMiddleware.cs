using System.Text.Json;
using FinFlow.Application.Common;
using FinFlow.Domain.Common;
using Microsoft.AspNetCore.Http;

namespace FinFlow.Api.Common;

/// <summary>Единый формат ошибок API (ProblemDetails) и журналирование необработанных исключений.</summary>
public class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException exception)
        {
            await WriteAsync(context, exception.StatusCode, exception.Message);
        }
        catch (DomainException exception)
        {
            await WriteAsync(context, 409, exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Необработанная ошибка при обработке {Path}", context.Request.Path);
            await WriteAsync(context, 500, "Внутренняя ошибка сервера. Обратитесь к администратору.");
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var payload = JsonSerializer.Serialize(new
        {
            status = statusCode,
            title = message,
            detail = message
        }, JsonOptions);

        await context.Response.WriteAsync(payload);
    }
}
