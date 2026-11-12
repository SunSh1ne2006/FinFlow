using FinFlow.Application.Abstractions;
using FinFlow.Application.Strategies;
using FinFlow.Application.Services;
using FinFlow.Infrastructure.Integrations;
using FinFlow.Infrastructure.Persistence;
using FinFlow.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinFlow.Infrastructure;

/// <summary>Регистрация слоёв приложения, инфраструктуры и интеграций (модульный монолит п. 2.4.1).</summary>
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
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddScoped<IAisIdoGateway, AisIdoGateway>();
        services.AddScoped<ILedgerGateway, LedgerGateway>();
        services.AddScoped<IMsiGateway, MsiGateway>();
        services.AddScoped<IEventPublisher, LoggingEventPublisher>();

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
        services.AddScoped<LiquidityService>();
        services.AddScoped<IReserveStrategy, P80ReserveStrategy>();
        services.AddScoped<RiskService>();
        services.AddScoped<MarginService>();
        services.AddScoped<AmlService>();
        services.AddScoped<IntegrationService>();
        services.AddScoped<ReportService>();
        services.AddScoped<AdminService>();
        services.AddScoped<MetaService>();

        return services;
    }

    /// <summary>Создание схемы и начальное заполнение базы при первом запуске.</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider provider, ILogger logger = null)
    {
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        await db.Database.EnsureCreatedAsync();
        await DbSeeder.SeedAsync(db, hasher);
        logger?.LogInformation("База данных FinFlow готова: схема создана, начальные данные загружены.");
    }
}
