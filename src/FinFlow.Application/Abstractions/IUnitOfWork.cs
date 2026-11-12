namespace FinFlow.Application.Abstractions;

/// <summary>
/// Единица работы: остаток и проводки изменяются в одной транзакции (требование 24).
/// </summary>
public interface IUnitOfWork
{
    Task<IUnitOfWorkTransaction> BeginAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
