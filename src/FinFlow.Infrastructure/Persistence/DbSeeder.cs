using FinFlow.Application.Abstractions;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Reference;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Persistence;

/// <summary>
/// Начальное заполнение базы: роли, сотрудники, параметры расчёта, продукты с версиями условий,
/// клиенты, счета, вклады, ограничения и демонстрационные операции.
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "FinFlow#2026!";

    public static async Task SeedAsync(BankDbContext db, IPasswordHasher hasher, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        await SeedRolesAsync(db, cancellationToken);
        await SeedUsersAsync(db, hasher, cancellationToken);
        await SeedParametersAsync(db, today, cancellationToken);

        if (!await db.Products.AnyAsync(cancellationToken))
        {
            await SeedProductsAsync(db, today, cancellationToken);
        }

        if (!await db.Clients.AnyAsync(cancellationToken))
        {
            await SeedClientsAsync(db, today, cancellationToken);
        }

        await SeedDemoOperationsAsync(db, today, cancellationToken);
    }

    private static async Task SeedRolesAsync(BankDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Roles.AnyAsync(cancellationToken))
        {
            return;
        }

        foreach (var role in ModelBankData.Roles)
        {
            db.Roles.Add(new Role
            {
                Code = role.Code,
                Name = role.Name,
                Description = role.Description
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedUsersAsync(BankDbContext db, IPasswordHasher hasher, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var roles = await db.Roles.ToDictionaryAsync(r => r.Code, cancellationToken);
        var passwordHash = hasher.Hash(DemoPassword);

        void AddUser(string login, string fullName, RoleCode roleCode)
        {
            var user = new User
            {
                Login = login,
                FullName = fullName,
                PasswordHash = passwordHash,
                IsActive = true
            };

            if (roles.TryGetValue(roleCode, out var role))
            {
                user.UserRoles.Add(new UserRole { User = user, Role = role });
            }

            db.Users.Add(user);
        }

        AddUser("employee1", "Сотрудник ЦБУ № 1 Иванова А. П.", RoleCode.Employee);
        AddUser("employee2", "Сотрудник ЦБУ № 2 Кузнецов Д. С.", RoleCode.Employee);
        AddUser("admin", "Администратор системы", RoleCode.Administrator);
        AddUser("head", "Руководитель отделения", RoleCode.Head);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedParametersAsync(BankDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        if (await db.Parameters.AnyAsync(cancellationToken))
        {
            return;
        }

        var validFrom = today.AddMonths(-6);

        void Add(string code, string name, string unit, decimal value)
        {
            db.Parameters.Add(new Parameter
            {
                Code = code,
                Name = name,
                Unit = unit,
                Value = value,
                ValidFrom = validFrom,
                IsActive = true
            });
        }

        Add("TaxRate", "Ставка подоходного налога с процентов", "%", ModelBankData.TaxRatePercent);
        Add("DemandRate", "Ставка до востребования", "% годовых", ModelBankData.DemandRatePercent);
        Add("RefinancingRate", "Ставка рефинансирования", "% годовых", ModelBankData.RefinancingRatePercent);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedProductsAsync(BankDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        var validFrom = today.AddMonths(-3);

        ProductVersion Version(decimal rate, DepositType depositType, bool revocable, int termMonths,
            CapitalizationMode capitalization, InterestPayoutMode payout, decimal minAmount, bool prolongation = false)
        {
            return new ProductVersion
            {
                Version = 1,
                RatePercent = rate,
                RateType = RateType.Fixed,
                DepositType = depositType,
                IsRevocable = revocable,
                AllowsProlongation = prolongation,
                TermMonths = termMonths,
                Capitalization = capitalization,
                InterestPayout = payout,
                MinAmount = minAmount,
                MinBalance = 0m,
                DayBase = 0,
                ValidFrom = validFrom,
                IsApproved = true,
                ApprovedAt = DateTime.UtcNow
            };
        }

        var current = new Product
        {
            Code = "BYN-TEKUCH",
            Name = "Текущий счёт «Удобный»",
            Kind = ProductKind.CurrentAccount,
            Currency = "BYN",
            BalanceAccount = "3012",
            Description = "Текущий счёт для расчётов, взносов и переводов. Проценты по ставке до востребования."
        };
        current.Versions.Add(Version(0.1m, DepositType.None, true, 0, CapitalizationMode.None, InterestPayoutMode.OnDemand, 0m));

        var stable = new Product
        {
            Code = "BYN-BEZOTZ",
            Name = "Безотзывный вклад «Стабильный»",
            Kind = ProductKind.Deposit,
            Currency = "BYN",
            BalanceAccount = "3014",
            Description = "Срочный безотзывный вклад с ежемесячной капитализацией процентов."
        };
        stable.Versions.Add(Version(11.8m, DepositType.Term, false, 12, CapitalizationMode.Monthly, InterestPayoutMode.AtEnd, 100m, true));

        var free = new Product
        {
            Code = "BYN-OTZYV",
            Name = "Отзывный вклад «Свободный»",
            Kind = ProductKind.Deposit,
            Currency = "BYN",
            BalanceAccount = "3014",
            Description = "Срочный отзывный вклад с ежемесячной выплатой процентов и перерасчётом по ставке до востребования при досрочном возврате."
        };
        free.Versions.Add(Version(9.0m, DepositType.Term, true, 6, CapitalizationMode.None, InterestPayoutMode.Monthly, 50m, true));

        var growth = new Product
        {
            Code = "BYN-NAKOP",
            Name = "Накопительный вклад «Рост»",
            Kind = ProductKind.Deposit,
            Currency = "BYN",
            BalanceAccount = "3014",
            Description = "Отзывный накопительный вклад: проценты выплачиваются в конце срока, возможна пролонгация."
        };
        growth.Versions.Add(Version(8.5m, DepositType.Term, true, 3, CapitalizationMode.None, InterestPayoutMode.AtEnd, 100m, true));

        var usd = new Product
        {
            Code = "USD-VKLAD",
            Name = "Валютный вклад «Долларовый»",
            Kind = ProductKind.Deposit,
            Currency = "USD",
            BalanceAccount = "3014",
            Description = "Срочный отзывный вклад в долларах США."
        };
        usd.Versions.Add(Version(1.5m, DepositType.Term, true, 12, CapitalizationMode.None, InterestPayoutMode.AtEnd, 100m, true));

        db.Products.AddRange(current, stable, free, growth, usd);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedClientsAsync(BankDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        var employees = await db.Users
            .Where(u => u.Login == "employee1" || u.Login == "employee2")
            .OrderBy(u => u.Login)
            .ToListAsync(cancellationToken);

        var employee1 = employees.FirstOrDefault(u => u.Login == "employee1");
        var employee2 = employees.FirstOrDefault(u => u.Login == "employee2") ?? employee1;

        Client NewClient(ClientType type, string name, string idNumber, DateOnly? birthDate, User createdBy)
        {
            return new Client
            {
                Type = type,
                Name = name,
                IdNumber = idNumber,
                BirthDate = birthDate,
                Country = type == ClientType.LegalEntity ? "BY" : "BY",
                IsResident = true,
                Phone = "+375 (29) 000-00-00",
                IdentificationStatus = IdentificationStatus.Identified,
                IdentifiedAt = DateTime.UtcNow.AddDays(-30),
                CreatedBy = createdBy?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            };
        }

        var ivanov = NewClient(ClientType.Individual, "Иванов Иван Иванович", "301AB1234", new DateOnly(1985, 3, 14), employee1);
        var petrova = NewClient(ClientType.Individual, "Петрова Анна Сергеевна", "302AB5678", new DateOnly(1990, 7, 22), employee1);
        var smirnova = NewClient(ClientType.Individual, "Смирнова Ольга Викторовна", "303AB9012", new DateOnly(1978, 11, 5), employee2);
        var sidorova = NewClient(ClientType.Individual, "Сидорова Мария Павловна", "304AB3456", new DateOnly(1969, 2, 18), employee1);
        var petrov = NewClient(ClientType.Individual, "Петров Дмитрий Олегович", "305AB7890", new DateOnly(1992, 9, 30), employee2);
        var vector = NewClient(ClientType.LegalEntity, "ООО «Вектор»", "191234567", null, employee2);
        var gorizont = NewClient(ClientType.LegalEntity, "ООО «Горизонт»", "192345678", null, employee1);
        var kovalev = NewClient(ClientType.Entrepreneur, "ИП Ковалёв А. С.", "193456789", null, employee1);

        db.Clients.AddRange(ivanov, petrova, smirnova, sidorova, petrov, vector, gorizont, kovalev);
        await db.SaveChangesAsync(cancellationToken);

        var products = await db.Products.Include(p => p.Versions).ToDictionaryAsync(p => p.Code, cancellationToken);
        var sequence = 0;

        Account OpenAccount(Client client, string productCode, decimal balance, decimal overdraft,
            DateOnly openDate, string prefix, User createdBy)
        {
            sequence += 1;
            var product = products[productCode];
            var version = product.Versions.OrderBy(v => v.Version).First();

            var account = new Account
            {
                ClientId = client.Id,
                BalanceAccount = product.BalanceAccount,
                Currency = product.Currency,
                Balance = balance,
                OverdraftLimit = overdraft,
                Status = AccountStatus.Open,
                CreatedBy = createdBy?.Id,
                CreatedAt = DateTime.UtcNow.AddDays(-Math.Min(20, Math.Max(0, today.DayNumber - openDate.DayNumber)))
            };

            account.Numbers.Add(new AccountNumber
            {
                Number = AccountNumberGenerator.Build(product.BalanceAccount, client.Id, sequence, NumberKind.Previous),
                Kind = NumberKind.Previous,
                ValidFrom = openDate
            });

            account.Numbers.Add(new AccountNumber
            {
                Number = AccountNumberGenerator.Build(product.BalanceAccount, client.Id, sequence, NumberKind.Modified),
                Kind = NumberKind.Modified,
                ValidFrom = AccountNumberGenerator.ModifiedNumbersStart
            });

            var endDate = product.Kind == ProductKind.CurrentAccount
                ? DateOnly.MaxValue
                : SchedulePolicy.AddMonthsClamped(openDate, Math.Max(1, version.TermMonths));

            var contract = new Contract
            {
                Number = $"{prefix}-{openDate:yyyyMMdd}-{sequence:D4}",
                ClientId = client.Id,
                Account = account,
                ProductVersionId = version.Id,
                OpenDate = openDate,
                EndDate = endDate,
                Principal = product.Kind == ProductKind.Deposit ? balance : 0m,
                Status = ContractStatus.Active,
                CreatedBy = createdBy?.Id,
                CreatedAt = account.CreatedAt
            };

            db.Accounts.Add(account);
            db.Contracts.Add(contract);

            if (product.Kind == ProductKind.Deposit && balance > 0m)
            {
                db.Operations.Add(new Operation
                {
                    AccountId = account.Id,
                    Account = account,
                    ContractId = contract.Id,
                    Contract = contract,
                    DocumentNumber = $"ВКЛ-{openDate:yyyyMMdd}-{sequence:D4}",
                    Kind = OperationKind.CashIn,
                    Amount = balance,
                    Currency = product.Currency,
                    Status = OperationStatus.Executed,
                    ExecutedAt = openDate.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc),
                    Description = "Первоначальный взнос по договору вклада",
                    CreatedBy = createdBy?.Id,
                    CreatedAt = openDate.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc)
                });
            }

            return account;
        }

        var ivanovCurrent = OpenAccount(ivanov, "BYN-TEKUCH", 12_000m, 5_000m, today.AddDays(-300), "ТС", employee1);
        ivanovCurrent.Holds.Add(new Hold
        {
            Source = HoldSource.Booking,
            BasisNumber = "БРОНЬ-2026-0142",
            BasisAuthority = "Операционное управление",
            Amount = 3_000m,
            IsArrest = false,
            ValidFrom = today.AddDays(-2),
            Status = HoldStatus.Active,
            Comment = "Бронирование под карточную операцию"
        });

        OpenAccount(petrova, "BYN-TEKUCH", -2_000m, 5_000m, today.AddDays(-420), "ТС", employee1);
        var sidorovaCurrent = OpenAccount(sidorova, "BYN-TEKUCH", 3_000m, 5_000m, today.AddDays(-150), "ТС", employee2);
        sidorovaCurrent.Holds.Add(new Hold
        {
            Source = HoldSource.Bank,
            BasisNumber = "АРЕСТ-2026-771",
            BasisAuthority = "Отдел принудительного исполнения",
            Amount = 4_000m,
            IsArrest = true,
            ValidFrom = today.AddDays(-5),
            Status = HoldStatus.Active,
            Comment = "Постановление об аресте средств"
        });

        OpenAccount(smirnova, "BYN-TEKUCH", 8_400m, 0m, today.AddDays(-90), "ТС", employee2);
        OpenAccount(petrov, "BYN-TEKUCH", 1_250m, 0m, today.AddDays(-60), "ТС", employee2);
        OpenAccount(vector, "BYN-TEKUCH", 45_000m, 20_000m, today.AddDays(-500), "ТС", employee2);
        OpenAccount(gorizont, "BYN-TEKUCH", 62_500m, 0m, today.AddDays(-380), "ТС", employee1);
        OpenAccount(kovalev, "BYN-TEKUCH", 7_300m, 10_000m, today.AddDays(-200), "ТС", employee1);

        OpenAccount(ivanov, "BYN-BEZOTZ", 30_000m, 0m, today.AddDays(-300), "ВКЛ", employee1);
        OpenAccount(ivanov, "BYN-OTZYV", 15_000m, 0m, today.AddDays(-35), "ВКЛ", employee1);
        OpenAccount(petrova, "BYN-BEZOTZ", 25_000m, 0m, today.AddDays(-210), "ВКЛ", employee1);
        OpenAccount(smirnova, "BYN-OTZYV", 20_000m, 0m, today.AddDays(-172), "ВКЛ", employee2);
        OpenAccount(kovalev, "BYN-OTZYV", 15_000m, 0m, today.AddDays(-40), "ВКЛ", employee1);
        OpenAccount(petrov, "USD-VKLAD", 2_500m, 0m, today.AddDays(-90), "ВКЛ", employee2);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoOperationsAsync(BankDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        if (await db.Operations.AnyAsync(cancellationToken))
        {
            return;
        }

        var employees = await db.Users
            .Where(u => u.Login == "employee1" || u.Login == "employee2")
            .OrderBy(u => u.Login)
            .ToListAsync(cancellationToken);

        var employee1 = employees.FirstOrDefault(u => u.Login == "employee1");
        var employee2 = employees.FirstOrDefault(u => u.Login == "employee2") ?? employee1;

        var accounts = await db.Accounts
            .Include(a => a.Client)
            .Include(a => a.Contracts)
            .Where(a => a.BalanceAccount == "3012")
            .ToListAsync(cancellationToken);

        Account Find(string name) =>
            accounts.FirstOrDefault(a => a.Client.Name.Contains(name, StringComparison.Ordinal));

        var sequence = 0;

        void AddOperation(Account account, OperationKind kind, decimal amount, string description, User author, int hoursAgo)
        {
            if (account == null)
            {
                return;
            }

            sequence += 1;
            var createdAt = DateTime.UtcNow.AddHours(-hoursAgo);

            db.Operations.Add(new Operation
            {
                AccountId = account.Id,
                Account = account,
                ContractId = account.Contracts.FirstOrDefault()?.Id,
                DocumentNumber = $"ПКО-{today:yyyyMMdd}-{sequence:D4}",
                Kind = kind,
                Amount = amount,
                Currency = account.Currency,
                Status = OperationStatus.Executed,
                Description = description,
                CreatedBy = author?.Id,
                CreatedAt = createdAt,
                ExecutedAt = createdAt,
                CheckedBalance = account.Balance,
                CheckedOverdraft = account.OverdraftLimit,
                CheckedHolds = 0m,
                CheckedAvailable = account.Balance + account.OverdraftLimit
            });
        }

        AddOperation(Find("Иванов"), OperationKind.CashIn, 2_500m, "Пополнение текущего счёта наличными", employee1, 30);
        AddOperation(Find("Сидорова"), OperationKind.TransferIn, 1_000m, "Безналичное поступление заработной платы", employee2, 26);
        AddOperation(Find("Вектор"), OperationKind.TransferOut, 1_000m, "Платёжное поручение № 217", employee2, 20);
        AddOperation(Find("Петрова"), OperationKind.CashIn, 2_000m, "Пополнение счёта наличными", employee1, 12);
        AddOperation(Find("Смирнова"), OperationKind.PartialWithdrawal, 500m, "Частичное снятие с текущего счёта", employee2, 4);

        await db.SaveChangesAsync(cancellationToken);
    }
}
