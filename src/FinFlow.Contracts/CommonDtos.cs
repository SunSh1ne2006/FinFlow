namespace FinFlow.Contracts;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Total { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class MessageDto
{
    public bool Success { get; set; }
    public string Message { get; set; }

    public static MessageDto Ok(string message) => new() { Success = true, Message = message };
    public static MessageDto Fail(string message) => new() { Success = false, Message = message };
}
