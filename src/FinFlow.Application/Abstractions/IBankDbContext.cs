using FinFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Abstractions;

/// <summary>Доступ к наборам сущностей банка (реализуется BankDbContext в слое Infrastructure).</summary>
public interface IBankDbContext
{
    DbSet<Client> Clients { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductVersion> ProductVersions { get; }
    DbSet<Account> Accounts { get; }
    DbSet<AccountNumber> AccountNumbers { get; }
    DbSet<Hold> Holds { get; }
    DbSet<Contract> Contracts { get; }
    DbSet<Operation> Operations { get; }
    DbSet<InterestAccrual> InterestAccruals { get; }
    DbSet<TaxWithholding> TaxWithholdings { get; }
    DbSet<OperationalDay> OperationalDays { get; }
    DbSet<Parameter> Parameters { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<AuditRecord> AuditRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
