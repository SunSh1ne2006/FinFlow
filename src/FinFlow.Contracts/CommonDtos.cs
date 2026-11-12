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

public class MetaDto
{
    public string ProductName { get; set; } = "FinFlow";
    public string Version { get; set; }
    public DateTime ServerTime { get; set; }
    public IReadOnlyList<RoleInfoDto> Roles { get; set; } = Array.Empty<RoleInfoDto>();
    public IReadOnlyList<string> Currencies { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> OperationKinds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> HoldSources { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> DepositTypes { get; set; } = Array.Empty<string>();
    public decimal OperatorLimit { get; set; }
    public decimal ControllerLimit { get; set; }
    public decimal AmlThreshold { get; set; }
    public string RvsrNote { get; set; }
}

public class RoleInfoDto
{
    public int Code { get; set; }
    public string CodeName { get; set; }
    public string Name { get; set; }
    public string Client { get; set; }
    public string Description { get; set; }
}
