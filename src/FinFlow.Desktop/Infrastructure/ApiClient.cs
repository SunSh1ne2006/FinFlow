using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinFlow.Desktop.Infrastructure;

/// <summary>HTTP-клиент WPF-приложения: токен доступа, JSON, разбор ошибок ProblemDetails.</summary>
public class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public ApiClient(string baseUrl)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string Token { get; private set; }

    public void SetToken(string token)
    {
        Token = token;
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        using var response = await _http.GetAsync(url, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    public async Task<T> PostAsync<T>(string url, object body = null, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(url, body ?? new { }, JsonOptions, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    public async Task<T> PutAsync<T>(string url, object body, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PutAsJsonAsync(url, body, JsonOptions, cancellationToken);
        return await ReadAsync<T>(response, cancellationToken);
    }

    public async Task PostAsync(string url, object body = null, CancellationToken cancellationToken = default)
    {
        using var response = await _http.PostAsJsonAsync(url, body ?? new { }, JsonOptions, cancellationToken);
        await ReadAsync<object>(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = ExtractMessage(content) ?? $"Ошибка сервера ({(int)response.StatusCode}).";
            throw new ApiException(message, (int)response.StatusCode);
        }

        if (typeof(T) == typeof(object) || string.IsNullOrWhiteSpace(content))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    private static string ExtractMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.TryGetProperty("title", out var title))
            {
                return title.GetString();
            }

            if (document.RootElement.TryGetProperty("detail", out var detail))
            {
                return detail.GetString();
            }
        }
        catch (JsonException)
        {
            return content;
        }

        return null;
    }
}
