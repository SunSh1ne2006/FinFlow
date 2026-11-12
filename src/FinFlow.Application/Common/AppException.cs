namespace FinFlow.Application.Common;

/// <summary>Ошибка прикладного уровня с HTTP-кодом ответа.</summary>
public class AppException : Exception
{
    public int StatusCode { get; }

    public AppException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message, 404)
    {
    }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message, 403)
    {
    }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message, 409)
    {
    }
}
