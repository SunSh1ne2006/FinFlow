using FinFlow.Application.Abstractions;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Reference;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Infrastructure.Persistence;

/// <summary>
/// Начальное заполнение базы: роли, пользователи, справочник параметров, продукты с версиями условий,
/// модельный портфель клиентов и счетов, реестр рисков R-01 – R-12, демонстрационные операции.
/// Все количественные характеристики — модельные допущения п. 1.3–1.9 пояснительной записки.
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

        if (!await db.Risks.AnyAsync(cancellationToken))
        {
            await SeedRisksAsync(db, today, cancellationToken);
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

        void AddUser(string login, string fullName, RoleCode roleCode, bool twoFactor)
        {
            var user = new User
            {
                Login = login,
                FullName = fullName,
                PasswordHash = passwordHash,
                TwoFactorEnabled = twoFactor,
                IsActive = true
            };

            if (roles.TryGetValue(roleCode, out var role))
            {
                user.UserRoles.Add(new UserRole { User = user, Role = role });
            }

            db.Users.Add(user);
        }

        AddUser("operator1", "Операционист ЦБУ № 1", RoleCode.Operator, false);
        AddUser("controller1", "Контролёр операционного управления", RoleCode.Controller, true);
        AddUser("accountant1", "Бухгалтер операционного управления", RoleCode.Accountant, false);
        AddUser("admin", "Администратор системы", RoleCode.Administrator, true);
        AddUser("head", "Руководитель – член КУАП", RoleCode.Head, false);
        AddUser("treasurer", "Специалист казначейства", RoleCode.Treasurer, false);
        AddUser("riskmanager", "Риск-менеджер", RoleCode.RiskManager, false);
        AddUser("aml", "Специалист по ПОД/ФТ", RoleCode.AmlOfficer, false);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedParametersAsync(BankDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        if (await db.Parameters.AnyAsync(cancellationToken))
        {
            return;
        }

        var validFrom = today.AddMonths(-1);

        void Add(string code, string name, string unit, decimal value, string textValue = null)
        {
            db.Parameters.Add(new Parameter
            {
                Code = code,
                Name = name,
                Unit = unit,
                Value = value,
                TextValue = textValue,
                ValidFrom = validFrom,
                IsActive = true
            });
        }

        Add("TaxRate", "Ставка подоходного налога с процентов", "%", ModelBankData.TaxRatePercent);
        Add("DemandRate", "Ставка до востребования", "% годовых", ModelBankData.DemandRatePercent);
        Add("RefinancingRate", "Ставка рефинансирования", "% годовых", ModelBankData.RefinancingRatePercent);
        Add("RvsrByn1To3", "РВСР по вкладам в BYN сроком до 3 лет", "% годовых", ModelBankData.RvsrByn1To3Years);
        Add("RvsrBynOver3", "РВСР по вкладам в BYN сроком свыше 3 лет", "% годовых", ModelBankData.RvsrBynOver3Years);
        Add("OperatorLimit", "Лимит операциониста без подтверждения", "р.", ModelBankData.OperatorLimit);
        Add("ControllerLimit", "Лимит контролёра", "р.", ModelBankData.ControllerLimit);
        Add("AmlThreshold", "Порог обязательного контроля ПОД/ФТ", "р.", ModelBankData.AmlThreshold);
        Add("ProlongationShare", "Доля пролонгации вкладов", "доля", ModelBankData.ProlongationShare);
        Add("EarlyWithdrawalShare", "Дневная доля досрочного изъятия отзывных вкладов", "доля", ModelBankData.EarlyWithdrawalShare);
        Add("OpeningCash", "Запас ликвидности на начало прогноза", "р.", 20_000_000m);
        Add("MinimumCash", "Минимальный запас ликвидности (КУАП)", "р.", 10_000_000m);
        Add("ExpectedDailyInflow", "Ожидаемые поступления дня", "р.", 2_000_000m);
        Add("TransferRate", "Трансфертная ставка размещения ресурсов", "% годовых", ModelBankData.TransferRatePercent);
        Add("OperatingCost", "Операционные расходы на обслуживание портфеля", "% от объёма", ModelBankData.OperatingCostPercent);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedProductsAsync(BankDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        var validFrom = today.AddMonths(-1);

        ProductVersion Version(decimal rate, DepositType depositType, bool revocable, int termMonths,
            CapitalizationMode capitalization, InterestPayoutMode payout, decimal minAmount, decimal minBalance,
            bool prolongation = false, RateType rateType = RateType.Fixed, decimal spread = 0m)
        {
            return new ProductVersion
            {
                Version = 1,
                RatePercent = rate,
                RateType = rateType,
                SpreadPercent = spread,
                DepositType = depositType,
                IsRevocable = revocable,
                AllowsProlongation = prolongation,
                TermMonths = termMonths,
                Capitalization = capitalization,
                InterestPayout = payout,
                MinAmount = minAmount,
                MinBalance = minBalance,
                DayBase = 0,
                ValidFrom = validFrom,
                IsApproved = true,
                ApprovedAt = DateTime.UtcNow,
                RvsrLimit = ModelBankData.RvsrByn1To3Years,
                Notes = "Модельная версия условий (допущение п. 1.11)."
            };
        }

        var irrevocable = new Product
        {
            Code = "BYN-BEZOTZ",
            Name = "Безотзывный вклад «Стабильный»",
            Kind = ProductKind.Deposit,
            Currency = "BYN",
            BalanceAccount = "3014",
            Description = "Срочный безотзывный вклад с ежемесячной капитализацией процентов."
        };
        irrevocable.Versions.Add(Version(11.8m, DepositType.Term, false, 12, CapitalizationMode.Monthly, InterestPayoutMode.AtEnd, 100m, 0m, true));

        var revocable = new Product
        {
            Code = "BYN-OTZYV",
            Name = "Отзывный вклад «Свободный»",
            Kind = ProductKind.Deposit,
            Currency = "BYN",
            BalanceAccount = "3014",
            Description = "Срочный отзывный вклад с ежемесячной выплатой процентов и перерасчётом по ставке до востребования при досрочном возврате."
        };
        revocable.Versions.Add(Version(9.0m, DepositType.Term, true, 6, CapitalizationMode.None, InterestPayoutMode.Monthly, 50m, 0m, true));

        var currency = new Product
        {
            Code = "USD-VKLAD",
            Name = "Валютный вклад «Долларовый»",
            Kind = ProductKind.Deposit,
            Currency = "USD",
            BalanceAccount = "3014",
            Description = "Срочный отзывный вклад в долларах США."
        };
        currency.Versions.Add(Version(1.5m, DepositType.Term, true, 12, CapitalizationMode.None, InterestPayoutMode.AtEnd, 100m, 0m, true));

        var current = new Product
        {
            Code = "BYN-TEKUCH",
            Name = "Текущий счёт физического лица",
            Kind = ProductKind.CurrentAccount,
            Currency = "BYN",
            BalanceAccount = "3012",
            Description = "Текущий счёт с начислением процентов по ставке до востребования, овердрафтом и блокировками."
        };
        current.Versions.Add(Version(0.1m, DepositType.None, true, 0, CapitalizationMode.None, InterestPayoutMode.Monthly, 0m, 0m));

        db.Products.AddRange(irrevocable, revocable, currency, current);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedClientsAsync(BankDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        var products = await db.Products.Include(p => p.Versions).ToListAsync(cancellationToken);
        var currentProduct = products.First(p => p.Code == "BYN-TEKUCH");
        var irrevocable = products.First(p => p.Code == "BYN-BEZOTZ");
        var revocable = products.First(p => p.Code == "BYN-OTZYV");
        var usdProduct = products.First(p => p.Code == "USD-VKLAD");

        var sequence = 0;

        Client NewClient(ClientType type, string name, string idNumber, KycRiskLevel risk, IdentificationStatus status = IdentificationStatus.Identified)
        {
            return new Client
            {
                Type = type,
                Name = name,
                IdNumber = idNumber,
                Phone = "+375 (29) 1" + (1000000 + sequence).ToString().Substring(1, 6),
                Email = "client" + (++sequence) + "@example.by",
                Country = "BY",
                IsResident = true,
                IdentificationStatus = status,
                IdentifiedAt = DateTime.UtcNow.AddDays(-200),
                KycProfile = new KycProfile
                {
                    RiskLevel = risk,
                    ReviewDate = today.AddMonths(-2),
                    Source = "ЦБУ",
                    IsActual = true,
                    UpdatedAt = DateTime.UtcNow.AddMonths(-2)
                }
            };
        }

        var ivanov = NewClient(ClientType.Individual, "Иванов Иван Иванович", "301AB1234", KycRiskLevel.Low);
        var petrova = NewClient(ClientType.Individual, "Петрова Анна Сергеевна", "302AB5678", KycRiskLevel.Low);
        var smirnova = NewClient(ClientType.Individual, "Смирнова Ольга Викторовна", "303AB9012", KycRiskLevel.Medium);
        var sidorova = NewClient(ClientType.Individual, "Сидорова Мария Павловна", "304AB3456", KycRiskLevel.High);
        var petrov = NewClient(ClientType.Individual, "Петров Дмитрий Олегович", "305AB7890", KycRiskLevel.Low);
        var vector = NewClient(ClientType.LegalEntity, "ООО «Вектор»", "191234567", KycRiskLevel.Medium);
        var gorizont = NewClient(ClientType.LegalEntity, "ООО «Горизонт»", "192345678", KycRiskLevel.Low);
        var kovalev = NewClient(ClientType.Entrepreneur, "ИП Ковалёв А. С.", "193456789", KycRiskLevel.Medium);

        db.Clients.AddRange(ivanov, petrova, smirnova, sidorova, petrov, vector, gorizont, kovalev);
        await db.SaveChangesAsync(cancellationToken);

        var accountSequence = 0;

        Account OpenAccount(Client client, Product product, ProductVersion version, decimal balance, decimal overdraft,
            DateOnly openDate, string contractPrefix, out Contract contract)
        {
            accountSequence += 1;

            var account = new Account
            {
                ClientId = client.Id,
                BalanceAccount = product.BalanceAccount,
                Currency = product.Currency,
                Balance = balance,
                OverdraftLimit = overdraft,
                Status = AccountStatus.Open
            };

            account.Numbers.Add(new AccountNumber
            {
                Number = AccountNumberGenerator.Build(product.BalanceAccount, client.Id, accountSequence, NumberKind.Previous),
                Kind = NumberKind.Previous,
                ValidFrom = openDate
            });

            account.Numbers.Add(new AccountNumber
            {
                Number = AccountNumberGenerator.Build(product.BalanceAccount, client.Id, accountSequence, NumberKind.Modified),
                Kind = NumberKind.Modified,
                ValidFrom = AccountNumberGenerator.ModifiedNumbersStart
            });

            var termMonths = version.TermMonths > 0 ? version.TermMonths : 12;
            var endDate = product.Kind == ProductKind.CurrentAccount
                ? DateOnly.MaxValue
                : SchedulePolicy.AddMonthsClamped(openDate, termMonths);

            contract = new Contract
            {
                Number = $"{contractPrefix}-{openDate:yyyyMMdd}-{accountSequence:D4}",
                ClientId = client.Id,
                Account = account,
                ProductVersionId = version.Id,
                OpenDate = openDate,
                EndDate = endDate,
                Principal = balance,
                Status = ContractStatus.Active
            };

            db.Accounts.Add(account);
            db.Contracts.Add(contract);
            return account;
        }

        // Пример 1 п. 1.3.1: B = 12 000 р., L = 5 000 р., H = 3 000 р. → D = 14 000 р.
        var ivanovCurrent = OpenAccount(ivanov, currentProduct, currentProduct.Versions[0], 12_000m, 5_000m, today.AddDays(-300), "ТС", out _);
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

        // Пример 2 п. 1.3.1: B = −2 000 р., L = 5 000 р. → D = 3 000 р., Kовд = 40 %.
        OpenAccount(petrova, currentProduct, currentProduct.Versions[0], -2_000m, 5_000m, today.AddDays(-420), "ТС", out _);

        // Пример 3 п. 1.3.1: B = 3 000 р., арест H = 4 000 р., L = 0 → D = 0.
        var sidorovaCurrent = OpenAccount(sidorova, currentProduct, currentProduct.Versions[0], 3_000m, 5_000m, today.AddDays(-150), "ТС", out _);
        sidorovaCurrent.Holds.Add(new Hold
        {
            Source = HoldSource.AisIdo,
            BasisNumber = "АИС-ИДО-2026-771",
            BasisAuthority = "Отдел принудительного исполнения",
            Amount = 4_000m,
            IsArrest = true,
            ValidFrom = today.AddDays(-5),
            Status = HoldStatus.Active,
            Comment = "Постановление об аресте средств"
        });

        OpenAccount(smirnova, currentProduct, currentProduct.Versions[0], 8_400m, 0m, today.AddDays(-90), "ТС", out _);
        OpenAccount(petrov, currentProduct, currentProduct.Versions[0], 1_250m, 0m, today.AddDays(-60), "ТС", out _);
        OpenAccount(vector, currentProduct, currentProduct.Versions[0], 45_000m, 20_000m, today.AddDays(-500), "ТС", out _);
        OpenAccount(gorizont, currentProduct, currentProduct.Versions[0], 62_500m, 0m, today.AddDays(-380), "ТС", out _);
        OpenAccount(kovalev, currentProduct, currentProduct.Versions[0], 7_300m, 10_000m, today.AddDays(-200), "ТС", out _);

        // Вклады: безотзывный (12 мес.), отзывный (6 мес., срок истекает через ~10 дней), валютный.
        OpenAccount(ivanov, irrevocable, irrevocable.Versions[0], 30_000m, 0m, today.AddDays(-300), "ВКЛ", out _);
        OpenAccount(smirnova, revocable, revocable.Versions[0], 20_000m, 0m, today.AddDays(-172), "ВКЛ", out _);
        OpenAccount(kovalev, revocable, revocable.Versions[0], 15_000m, 0m, today.AddDays(-40), "ВКЛ", out _);
        OpenAccount(petrov, usdProduct, usdProduct.Versions[0], 2_500m, 0m, today.AddDays(-90), "ВКЛ", out _);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRisksAsync(BankDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        foreach (var model in ModelBankData.Risks)
        {
            var risk = new Risk
            {
                Code = model.Code,
                Event = model.Event,
                Cause = model.Cause,
                Consequence = model.Consequence,
                Category = model.Category,
                OwnerRole = model.OwnerRole,
                Status = RiskStatus.Active,
                Mitigation = model.Mitigation,
                EarlyIndicator = model.EarlyIndicator
            };

            risk.Assessments.Add(new RiskAssessment
            {
                Date = today,
                Probability = model.Probability,
                MoneyImpact = model.MoneyImpact,
                MitigatedProbability = model.MitigatedProbability,
                Notes = "Экспертная оценка (таблицы 1.15, 1.17)."
            });

            db.Risks.Add(risk);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedDemoOperationsAsync(BankDbContext db, DateOnly today, CancellationToken cancellationToken)
    {
        if (await db.Operations.AnyAsync(cancellationToken))
        {
            return;
        }

        var operatorUser = await db.Users.FirstOrDefaultAsync(u => u.Login == "operator1", cancellationToken);
        if (operatorUser == null)
        {
            return;
        }

        var accounts = await db.Accounts
            .Include(a => a.Client)
            .Include(a => a.Contracts)
            .ToListAsync(cancellationToken);

        var ivanov = accounts.FirstOrDefault(a => a.Client.Name.Contains("Иванов") && a.BalanceAccount == "3012");
        var sidorova = accounts.FirstOrDefault(a => a.Client.Name.Contains("Сидорова") && a.BalanceAccount == "3012");
        var vector = accounts.FirstOrDefault(a => a.Client.Name.Contains("Вектор") && a.BalanceAccount == "3012");
        var petrova = accounts.FirstOrDefault(a => a.Client.Name.Contains("Петрова") && a.BalanceAccount == "3012");

        var sequence = 0;

        Operation NewOperation(Account account, OperationKind kind, decimal amount, OperationStatus status,
            string description, DateTime createdAt, string sourceNumber)
        {
            sequence += 1;
            var document = new OperationDocument
            {
                Number = sourceNumber,
                SourceSystem = DocumentSource.Branch,
                Kind = kind.ToString(),
                ReceivedAt = createdAt,
                ClientId = account.ClientId
            };

            var operation = new Operation
            {
                Account = account,
                AccountId = account.Id,
                Contract = account.Contracts.FirstOrDefault(),
                ContractId = account.Contracts.FirstOrDefault()?.Id,
                Document = document,
                Kind = kind,
                Amount = amount,
                Currency = account.Currency,
                Status = status,
                Description = description,
                CreatedBy = operatorUser.Id,
                CreatedAt = createdAt,
                CheckedBalance = account.Balance,
                CheckedOverdraft = account.OverdraftLimit,
                CheckedHolds = 0m,
                CheckedAvailable = account.Balance + account.OverdraftLimit,
                IdempotencyKey = Guid.NewGuid().ToString("N")
            };

            db.Operations.Add(operation);
            return operation;
        }

        if (ivanov != null)
        {
            NewOperation(ivanov, OperationKind.CashOut, 8_000m, OperationStatus.OnControl,
                "Выдача наличных: сумма выше лимита операциониста", DateTime.UtcNow.AddMinutes(-40), $"ПКО-{today:yyyyMMdd}-0001");
        }

        if (sidorova != null)
        {
            NewOperation(sidorova, OperationKind.CashIn, 25_000m, OperationStatus.Suspended,
                "Взнос наличными: сумма выше порога ПОД/ФТ, клиент высокого риска", DateTime.UtcNow.AddMinutes(-25), $"ПКО-{today:yyyyMMdd}-0002");
        }

        if (vector != null)
        {
            NewOperation(vector, OperationKind.TransferOut, 1_000m, OperationStatus.Draft,
                "Платёжное поручение № 217", DateTime.UtcNow.AddMinutes(-10), $"ПП-{today:yyyyMMdd}-0217");
        }

        if (petrova != null)
        {
            NewOperation(petrova, OperationKind.CashIn, 2_000m, OperationStatus.Executed,
                "Пополнение счёта наличными", DateTime.UtcNow.AddHours(-2), $"ПКО-{today:yyyyMMdd}-0003");
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
