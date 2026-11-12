using FinFlow.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FinFlow.Infrastructure.Persistence;

/// <summary>Единица работы поверх транзакции EF Core: остаток и проводки изменяются атомарно.</summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly BankDbContext _db;

    public UnitOfWork(BankDbContext db)
    {
        _db = db;
    }

    public async Task<IUnitOfWorkTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        return new EfUnitOfWorkTransaction(transaction);
    }

    private sealed class EfUnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public EfUnitOfWorkTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default) => _transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) => _transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }
}
