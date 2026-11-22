using FinFlow.Application.Abstractions;
using FinFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Persistence;

/// <summary>
/// Контекст базы данных (Microsoft SQL Server, EF Core 10).
/// Уникальные индексы, ограничения CHECK, decimal(18,4) для сумм и decimal(9,6) для ставок.
/// </summary>
public class BankDbContext : DbContext, IBankDbContext
{
    public BankDbContext(DbContextOptions<BankDbContext> options) : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVersion> ProductVersions => Set<ProductVersion>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountNumber> AccountNumbers => Set<AccountNumber>();
    public DbSet<Hold> Holds => Set<Hold>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Operation> Operations => Set<Operation>();
    public DbSet<InterestAccrual> InterestAccruals => Set<InterestAccrual>();
    public DbSet<TaxWithholding> TaxWithholdings => Set<TaxWithholding>();
    public DbSet<OperationalDay> OperationalDays => Set<OperationalDay>();
    public DbSet<Parameter> Parameters => Set<Parameter>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureClients(modelBuilder);
        ConfigureProducts(modelBuilder);
        ConfigureAccounts(modelBuilder);
        ConfigureContracts(modelBuilder);
        ConfigureOperations(modelBuilder);
        ConfigureAccruals(modelBuilder);
        ConfigureReferences(modelBuilder);
        ConfigureSecurity(modelBuilder);
    }

    private static void ConfigureClients(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("Clients");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.IdNumber).HasMaxLength(50).IsRequired();
            entity.Property(x => x.BirthDate).HasColumnType("date");
            entity.Property(x => x.Country).HasMaxLength(2);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Email).HasMaxLength(100);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.Type, x.IdNumber }).IsUnique();
            entity.HasIndex(x => x.Name);
        });
    }

    private static void ConfigureProducts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.BalanceAccount).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<ProductVersion>(entity =>
        {
            entity.ToTable("ProductVersions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RatePercent).HasPrecision(9, 6);
            entity.Property(x => x.SpreadPercent).HasPrecision(9, 6);
            entity.Property(x => x.MinAmount).HasPrecision(18, 4);
            entity.Property(x => x.MinBalance).HasPrecision(18, 4);
            entity.Property(x => x.Notes).HasMaxLength(500);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.ProductId, x.ValidFrom }).IsUnique();
            entity.HasOne(x => x.Product)
                .WithMany(x => x.Versions)
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProductVersions_Rate", "[RatePercent] >= 0");
                t.HasCheckConstraint("CK_ProductVersions_MinBalance", "[MinBalance] >= 0");
            });
        });
    }

    private static void ConfigureAccounts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("Accounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BalanceAccount).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Balance).HasPrecision(18, 4);
            entity.Property(x => x.OverdraftLimit).HasPrecision(18, 4);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.ClientId);
            entity.HasIndex(x => x.BalanceAccount);
            entity.HasOne(x => x.Client)
                .WithMany(x => x.Accounts)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_Accounts_Overdraft", "[OverdraftLimit] >= 0"));
        });

        modelBuilder.Entity<AccountNumber>(entity =>
        {
            entity.ToTable("AccountNumbers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).HasMaxLength(20).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.Number).IsUnique();
            entity.HasIndex(x => new { x.AccountId, x.Kind });
            entity.HasOne(x => x.Account)
                .WithMany(x => x.Numbers)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Hold>(entity =>
        {
            entity.ToTable("Holds");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.BasisNumber).HasMaxLength(50);
            entity.Property(x => x.BasisAuthority).HasMaxLength(200);
            entity.Property(x => x.Comment).HasMaxLength(500);
            entity.Property(x => x.Amount).HasPrecision(18, 4);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.AccountId, x.Status });
            entity.HasOne(x => x.Account)
                .WithMany(x => x.Holds)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Holds_Amount", "[Amount] > 0");
                t.HasCheckConstraint("CK_Holds_Period", "[ValidTo] IS NULL OR [ValidTo] >= [ValidFrom]");
            });
        });
    }

    private static void ConfigureContracts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Contract>(entity =>
        {
            entity.ToTable("Contracts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Number).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Principal).HasPrecision(18, 4);
            entity.Property(x => x.AccruedInterest).HasPrecision(18, 4);
            entity.Property(x => x.PaidInterest).HasPrecision(18, 4);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.Number).IsUnique();
            entity.HasIndex(x => new { x.EndDate, x.Status });
            entity.HasIndex(x => x.ClientId);
            entity.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Account)
                .WithMany(x => x.Contracts)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProductVersion)
                .WithMany()
                .HasForeignKey(x => x.ProductVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Contracts_Period", "[EndDate] >= [OpenDate]");
                t.HasCheckConstraint("CK_Contracts_Principal", "[Principal] >= 0");
            });
        });
    }

    private static void ConfigureOperations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Operation>(entity =>
        {
            entity.ToTable("Operations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DocumentNumber).HasMaxLength(50);
            entity.Property(x => x.Amount).HasPrecision(18, 4);
            entity.Property(x => x.Currency).HasMaxLength(3);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.RejectionReason).HasMaxLength(1000);
            entity.Property(x => x.CheckedBalance).HasPrecision(18, 4);
            entity.Property(x => x.CheckedOverdraft).HasPrecision(18, 4);
            entity.Property(x => x.CheckedHolds).HasPrecision(18, 4);
            entity.Property(x => x.CheckedAvailable).HasPrecision(18, 4);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.AccountId, x.CreatedAt });
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ReversalOfId);
            entity.HasOne(x => x.Account)
                .WithMany(x => x.Operations)
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Contract)
                .WithMany(x => x.Operations)
                .HasForeignKey(x => x.ContractId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_Operations_Amount", "[Amount] > 0"));
        });
    }

    private static void ConfigureAccruals(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InterestAccrual>(entity =>
        {
            entity.ToTable("InterestAccruals");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Base).HasPrecision(18, 4);
            entity.Property(x => x.Amount).HasPrecision(18, 4);
            entity.Property(x => x.RatePercent).HasPrecision(9, 6);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.ContractId, x.AccrualDate }).IsUnique();
            entity.HasIndex(x => x.AccrualDate);
            entity.HasOne(x => x.Contract)
                .WithMany(x => x.Accruals)
                .HasForeignKey(x => x.ContractId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t => t.HasCheckConstraint("CK_Accruals_Amount", "[Amount] >= 0"));
        });

        modelBuilder.Entity<TaxWithholding>(entity =>
        {
            entity.ToTable("TaxWithholdings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TaxBase).HasPrecision(18, 4);
            entity.Property(x => x.Amount).HasPrecision(18, 4);
            entity.Property(x => x.RatePercent).HasPrecision(9, 6);
            entity.Property(x => x.ExemptionReason).HasMaxLength(300);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.WithheldAt);
            entity.HasOne(x => x.Accrual)
                .WithMany()
                .HasForeignKey(x => x.AccrualId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Contract)
                .WithMany()
                .HasForeignKey(x => x.ContractId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_Tax_Amount", "[Amount] >= 0"));
        });

        modelBuilder.Entity<OperationalDay>(entity =>
        {
            entity.ToTable("OperationalDays");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.LastError).HasMaxLength(1000);
            entity.Property(x => x.InterestAccrued).HasPrecision(18, 4);
            entity.Property(x => x.TaxWithheld).HasPrecision(18, 4);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.Date).IsUnique();
        });
    }

    private static void ConfigureReferences(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Parameter>(entity =>
        {
            entity.ToTable("Parameters");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.Unit).HasMaxLength(30);
            entity.Property(x => x.Value).HasPrecision(18, 6);
            entity.Property(x => x.TextValue).HasMaxLength(500);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.Code, x.ValidFrom }).IsUnique();
        });

        modelBuilder.Entity<AuditRecord>(entity =>
        {
            entity.ToTable("AuditLog");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserLogin).HasMaxLength(50);
            entity.Property(x => x.Action).HasMaxLength(100);
            entity.Property(x => x.EntityName).HasMaxLength(100);
            entity.Property(x => x.EntityId).HasMaxLength(100);
            entity.Property(x => x.OldValue).HasMaxLength(4000);
            entity.Property(x => x.NewValue).HasMaxLength(4000);
            entity.Property(x => x.IpAddress).HasMaxLength(50);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.At);
            entity.HasIndex(x => new { x.EntityName, x.EntityId });
            entity.HasIndex(x => new { x.UserId, x.Action });
        });
    }

    private static void ConfigureSecurity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Login).HasMaxLength(50).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(300).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => x.Login).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("UserRoles");
            entity.HasKey(x => new { x.UserId, x.RoleId });
            entity.HasOne(x => x.User)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Role)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
