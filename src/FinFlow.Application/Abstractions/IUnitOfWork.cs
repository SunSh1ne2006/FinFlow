namespace FinFlow.Application.Abstractions;

/// <summary>
/// Единица работы: остаток счёта и операция изменяются в одной транзакции.
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
