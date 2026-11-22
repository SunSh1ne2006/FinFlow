using FinFlow.Application.Abstractions;
using FinFlow.Application.Services;
using FinFlow.Infrastructure.Persistence;
using FinFlow.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinFlow.Infrastructure;

/// <summary>Регистрация слоёв приложения и инфраструктуры.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddFinFlow(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BankDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3)));

        services.AddScoped<IBankDbContext>(provider => provider.GetRequiredService<BankDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITokenStore, InMemoryTokenStore>();
        services.AddSingleton<IClientSessionStore, InMemoryClientSessionStore>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddScoped<AuditService>();
        services.AddScoped<IAuditWriter>(provider => provider.GetRequiredService<AuditService>());

        services.AddScoped<AuthService>();
        services.AddScoped<ParameterService>();
        services.AddScoped<BalanceService>();
        services.AddScoped<IBalanceService>(provider => provider.GetRequiredService<BalanceService>());
        services.AddScoped<ClientService>();
        services.AddScoped<ProductService>();
        services.AddScoped<AccountService>();
        services.AddScoped<ContractService>();
        services.AddScoped<InterestService>();
        services.AddScoped<TaxService>();
        services.AddScoped<OperationService>();
        services.AddScoped<HoldService>();
        services.AddScoped<DayCloseService>();
        services.AddScoped<StaffReportService>();
        services.AddScoped<AdminService>();
        services.AddScoped<ClientPortalService>();

        return services;
    }

    /// <summary>Создание схемы и начальное заполнение базы при первом запуске.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider provider, ILogger logger = null)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await db.Database.EnsureCreatedAsync();
        await EnsureClientPortalSchemaAsync(db, logger);
        await DbSeeder.SeedAsync(db, hasher);
        logger?.LogInformation("База данных FinFlow готова: схема создана, начальные данные загружены.");
    }

    /// <summary>
    /// Совместимость с базой, созданной до появления личного кабинета клиента:
    /// EnsureCreated не изменяет существующую схему, поэтому отсутствующая колонка
    /// Clients.BirthDate добавляется отдельно (SQL Server).
    /// </summary>
    private static async Task EnsureClientPortalSchemaAsync(BankDbContext db, ILogger logger)
    {
        if (!db.Database.IsSqlServer())
        {
            return;
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "IF COL_LENGTH('Clients','BirthDate') IS NULL ALTER TABLE [Clients] ADD [BirthDate] date NULL;");
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception,
                "Не удалось проверить колонку Clients.BirthDate. Если личный кабинет клиента недоступен, создайте базу заново.");
        }
    }
}
