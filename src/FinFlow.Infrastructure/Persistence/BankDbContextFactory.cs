using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinFlow.Infrastructure.Persistence;

/// <summary>Фабрика контекста для команд EF Core (миграции, создание схемы).</summary>
public class BankDbContextFactory : IDesignTimeDbContextFactory<BankDbContext>
{
    public BankDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FINFLOW_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=FinFlow;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<BankDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new BankDbContext(options);
    }
}
