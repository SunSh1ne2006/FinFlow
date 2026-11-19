using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Contracts;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Common;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using FinFlow.Domain.Reference;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Личный кабинет клиента (сокращённая реализация требования 22 — интерфейс ДБО):
/// вход по паспортным данным, просмотр собственных вкладов, их сроков, сумм и
/// начисленных процентов, а также опубликованных условий вкладов банка.
/// Кабинет работает только на чтение: оформление выполняется в отделении.
/// </summary>
public class ClientPortalService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly IClientSessionStore _sessions;
    private readonly ParameterService _parameters;

    public ClientPortalService(
        IBankDbContext db,
        IClock clock,
        IClientSessionStore sessions,
        ParameterService parameters)
    {
        _db = db;
        _clock = clock;
        _sessions = sessions;
        _parameters = parameters;
    }

    /// <summary>Вход по серии и номеру паспорта и дате рождения.</summary>
    public async Task<ClientLoginResponse> LoginAsync(ClientLoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.DocumentNumber))
        {
            throw new AppException("Укажите серию и номер паспорта.");
        }

        var document = NormalizeDocument(request.DocumentNumber);
        var client = await FindClientAsync(document, cancellationToken);

        if (client == null)
        {
            throw new AppException("Клиент с указанными паспортными данными не найден.", 401);
        }

        if (client.Type != ClientType.Individual)
        {
            throw new ForbiddenException("Личный кабинет доступен только физическим лицам.");
        }

        if (!client.IsIdentified)
        {
            throw new ForbiddenException("Личный кабинет доступен после идентификации в отделении банка.");
        }

        var registered = false;
        if (client.BirthDate.HasValue)
        {
            if (!request.BirthDate.HasValue || client.BirthDate.Value != request.BirthDate.Value)
            {
                throw new AppException("Паспортные данные не совпадают: проверьте серию, номер и дату рождения.", 401);
            }
        }
        else
        {
            if (!request.BirthDate.HasValue)
            {
                throw new AppException("Укажите дату рождения, как в паспорте.");
            }

            client.BirthDate = request.BirthDate.Value;
            registered = true;
        }

        var issued = _sessions.Issue(client.Id, client.IdNumber, client.Name);

        // Вход клиента фиксируется в журнале аудита отдельной записью: клиент не является
        // пользователем системы, поэтому UserId не заполняется.
        _db.AuditRecords.Add(new AuditRecord
        {
            At = _clock.UtcNow,
            UserLogin = "client:" + MaskDocument(client.IdNumber),
            Action = "ClientPortalLogin",
            EntityName = nameof(Client),
            EntityId = client.Id.ToString(),
            NewValue = registered
                ? "Вход в личный кабинет; дата рождения зафиксирована"
                : "Вход в личный кабинет"
        });

        await _db.SaveChangesAsync(cancellationToken);

        return new ClientLoginResponse
        {
            Token = issued.Token,
            ExpiresAt = issued.ExpiresAt,
            BirthDateRegistered = registered,
            Client = ToProfile(client)
        };
    }

    public void Logout(string token) => _sessions.Revoke(token);

    public async Task<ClientPortalProfileDto> GetProfileAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client == null)
        {
            throw new NotFoundException("Клиент не найден.");
        }

        return ToProfile(client);
    }

    /// <summary>Все вклады клиента: действующие и закрытые, с суммами, сроками и доходом.</summary>
    public async Task<ClientDepositsDto> GetDepositsAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        var contracts = await _db.Contracts
            .Include(c => c.Account)
            .ThenInclude(a => a.Numbers)
            .Include(c => c.ProductVersion)
            .ThenInclude(v => v.Product)
            .Where(c => c.ClientId == clientId && c.ProductVersion.Product.Kind == ProductKind.Deposit)
            .ToListAsync(cancellationToken);

        var deposits = contracts
            .Select(c => ToDeposit(c, today))
            .OrderByDescending(d => d.IsActive)
            .ThenBy(d => d.EndDate)
            .ToList();

        var totals = deposits
            .Where(d => d.IsActive)
            .GroupBy(d => d.Currency)
            .Select(g => new ClientCurrencyTotalDto
            {
                Currency = g.Key,
                Principal = Money.Round(g.Sum(d => d.Principal)),
                AccruedInterest = Money.Round(g.Sum(d => d.AccruedInterest)),
                ExpectedIncome = Money.Round(g.Sum(d => d.ExpectedIncome)),
                DepositsCount = g.Count()
            })
            .OrderBy(t => t.Currency)
            .ToList();

        return new ClientDepositsDto
        {
            Deposits = deposits,
            Totals = totals,
            ActiveCount = deposits.Count(d => d.IsActive),
            ClosedCount = deposits.Count(d => !d.IsActive),
            AsOf = today
        };
    }

    /// <summary>Детали вклада: начисления, номера счёта и оценка досрочного возврата.</summary>
    public async Task<ClientDepositDetailsDto> GetDepositAsync(Guid clientId, Guid contractId, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        var contract = await _db.Contracts
            .Include(c => c.Account)
            .ThenInclude(a => a.Numbers)
            .Include(c => c.ProductVersion)
            .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);

        if (contract == null)
        {
            throw new NotFoundException("Вклад не найден.");
        }

        if (contract.ClientId != clientId)
        {
            throw new ForbiddenException("Вклад принадлежит другому клиенту.");
        }

        var accruals = await _db.InterestAccruals
            .Where(a => a.ContractId == contractId)
            .OrderByDescending(a => a.AccrualDate)
            .Take(12)
            .ToListAsync(cancellationToken);

        var baseDto = ToDeposit(contract, today);
        var version = contract.ProductVersion;
        var demandRate = await _parameters.GetValueAsync(ParameterService.DemandRate, today, ModelBankData.DemandRatePercent, cancellationToken);
        var elapsedDays = Math.Max(0, today.DayNumber - contract.OpenDate.DayNumber);
        var yearDays = version?.ResolveDayBase(today) ?? InterestCalculator.YearDays(today);

        var quote = EarlyReturnCalculator.Calculate(
            contract.Principal,
            version?.RatePercent ?? 0m,
            elapsedDays,
            yearDays,
            demandRate,
            contract.PaidInterest,
            version?.RequiresBankConsentForEarlyReturn == true,
            contract.HasEarlyReturnConsent);

        var earlyReturn = new ClientEarlyReturnEstimateDto
        {
            Available = contract.IsActive && quote.Allowed,
            RequiresBankConsent = quote.RequiresBankConsent,
            ConsentGranted = quote.ConsentGranted,
            Days = quote.Days,
            Payout = quote.NetPayout,
            ContractInterest = quote.ContractInterest,
            DemandInterest = quote.DemandInterest,
            Withheld = quote.ExcessToWithhold,
            Note = quote.Allowed
                ? "При досрочном возврате проценты пересчитываются по ставке до востребования."
                : quote.Reason
        };

        return new ClientDepositDetailsDto
        {
            ContractId = baseDto.ContractId,
            ContractNumber = baseDto.ContractNumber,
            AccountNumber = baseDto.AccountNumber,
            ProductName = baseDto.ProductName,
            ProductDescription = baseDto.ProductDescription,
            Currency = baseDto.Currency,
            Principal = baseDto.Principal,
            AccruedInterest = baseDto.AccruedInterest,
            PaidInterest = baseDto.PaidInterest,
            RatePercent = baseDto.RatePercent,
            RateTypeText = baseDto.RateTypeText,
            TermMonths = baseDto.TermMonths,
            TermDays = baseDto.TermDays,
            OpenDate = baseDto.OpenDate,
            EndDate = baseDto.EndDate,
            DaysLeft = baseDto.DaysLeft,
            ProgressPercent = baseDto.ProgressPercent,
            Status = baseDto.Status,
            StatusText = baseDto.StatusText,
            IsActive = baseDto.IsActive,
            IsRevocable = baseDto.IsRevocable,
            AllowsProlongation = baseDto.AllowsProlongation,
            CapitalizationText = baseDto.CapitalizationText,
            InterestPayoutText = baseDto.InterestPayoutText,
            NextInterestDate = baseDto.NextInterestDate,
            ExpectedIncome = baseDto.ExpectedIncome,
            ExpectedTotal = baseDto.ExpectedTotal,
            CanEarlyReturn = baseDto.CanEarlyReturn,
            EarlyReturnNote = baseDto.EarlyReturnNote,
            Balance = contract.Account?.Balance ?? 0m,
            AccountNumbers = contract.Account?.Numbers
                .OrderBy(n => n.ValidFrom)
                .Select(n => n.Kind == NumberKind.Modified ? n.Number + " (с 01.01.2027)" : n.Number)
                .ToList() ?? new List<string>(),
            Accruals = accruals.Select(a => new ClientAccrualDto
            {
                Date = a.AccrualDate,
                Base = a.Base,
                RatePercent = a.RatePercent,
                Amount = a.Amount,
                Status = a.Status.Text()
            }).ToList(),
            EarlyReturn = earlyReturn,
            TaxNote = version == null
                ? null
                : TaxCalculator.IsExempt(contract.Account?.Currency ?? "BYN", contract.TermDays)
                    ? "Проценты по вкладу освобождены от подоходного налога."
                    : "С процентов удерживается подоходный налог 13 % (формула 1.8)."
        };
    }

    /// <summary>Опубликованные условия вкладов: ставка, срок, минимальная сумма, порядок выплаты.</summary>
    public async Task<ClientOffersDto> GetOffersAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        var today = _clock.Today;

        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client == null)
        {
            throw new NotFoundException("Клиент не найден.");
        }

        var products = await _db.Products
            .Include(p => p.Versions)
            .Where(p => p.Kind == ProductKind.Deposit && p.IsActive)
            .ToListAsync(cancellationToken);

        var rvsr1 = await _parameters.GetValueAsync(ParameterService.RvsrByn1To3, today, ModelBankData.RvsrByn1To3Years, cancellationToken);
        var rvsr2 = await _parameters.GetValueAsync(ParameterService.RvsrBynOver3, today, ModelBankData.RvsrBynOver3Years, cancellationToken);
        var demandRate = await _parameters.GetValueAsync(ParameterService.DemandRate, today, ModelBankData.DemandRatePercent, cancellationToken);

        var offers = new List<ClientOfferDto>();

        foreach (var product in products)
        {
            var terms = product.Versions
                .Where(v => v.IsApproved && v.ValidFrom <= today)
                .GroupBy(v => v.TermMonths)
                .Select(g => g.OrderByDescending(v => v.ValidFrom).ThenByDescending(v => v.Version).First())
                .OrderBy(v => v.TermMonths)
                .ToList();

            if (terms.Count == 0)
            {
                continue;
            }

            var best = terms.OrderByDescending(v => v.RatePercent).First();

            offers.Add(new ClientOfferDto
            {
                ProductId = product.Id,
                Code = product.Code,
                Name = product.Name,
                Description = product.Description,
                Currency = product.Currency,
                MinAmount = terms.Min(v => v.MinAmount),
                IsRevocable = terms.Any(v => v.IsRevocable),
                AllowsProlongation = terms.Any(v => v.AllowsProlongation),
                CapitalizationText = best.Capitalization.Text(),
                InterestPayoutText = best.InterestPayout.Text(),
                BestRate = best.RatePercent,
                MinTermMonths = terms.Min(v => v.TermMonths),
                MaxTermMonths = terms.Max(v => v.TermMonths),
                Note = "Оформление — в отделении банка; условия фиксируются в договоре.",
                Terms = terms.Select(v =>
                {
                    var termDays = Math.Max(1, SchedulePolicy.AddMonthsClamped(today, v.TermMonths).DayNumber - today.DayNumber);
                    return new ClientOfferTermDto
                    {
                        VersionId = v.Id,
                        RatePercent = v.RatePercent,
                        TermMonths = v.TermMonths,
                        TermDays = termDays,
                        MinAmount = v.MinAmount,
                        MinBalance = v.MinBalance,
                        IsRevocable = v.IsRevocable,
                        InterestPayoutText = v.InterestPayout.Text(),
                        CapitalizationText = v.Capitalization.Text(),
                        IncomePer1000 = InterestCalculator.AccrueFlat(1000m, v.RatePercent, termDays, 365),
                        RateWithinRvsr = !string.Equals(product.Currency, "BYN", StringComparison.OrdinalIgnoreCase)
                            || v.RatePercent <= (v.TermMonths <= 36 ? rvsr1 : rvsr2)
                    };
                }).ToList()
            });
        }

        return new ClientOffersDto
        {
            Offers = offers.OrderBy(o => o.Currency).ThenByDescending(o => o.BestRate).ToList(),
            DemandRatePercent = demandRate,
            RvsrByn1To3Years = rvsr1,
            RvsrBynOver3Years = rvsr2,
            AsOf = today,
            Note = "Ставки указаны в процентах годовых и действуют на дату расчёта. "
                 + "Доход на 1 000 р. рассчитан по формуле простых процентов (1.5) за срок вклада."
        };
    }

    private async Task<Client> FindClientAsync(string document, CancellationToken cancellationToken)
    {
        var exact = await _db.Clients
            .FirstOrDefaultAsync(c => c.IdNumber == document, cancellationToken);

        if (exact != null)
        {
            return exact;
        }

        var candidates = await _db.Clients
            .Where(c => c.Type == ClientType.Individual)
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(c => NormalizeDocument(c.IdNumber) == document);
    }

    private ClientDepositDto ToDeposit(Contract contract, DateOnly today)
    {
        var version = contract.ProductVersion;
        var currency = contract.Account?.Currency ?? version?.Product?.Currency ?? "BYN";
        var rate = version?.RatePercent ?? 0m;
        var termDays = contract.EndDate == DateOnly.MaxValue ? 0 : contract.TermDays;
        var daysLeft = contract.IsActive && contract.EndDate != DateOnly.MaxValue
            ? Math.Max(0, contract.EndDate.DayNumber - today.DayNumber)
            : 0;

        var totalDays = Math.Max(1, termDays);
        var elapsed = Math.Max(0, today.DayNumber - contract.OpenDate.DayNumber);
        var progress = contract.EndDate == DateOnly.MaxValue
            ? 0m
            : Math.Clamp(Math.Round(elapsed * 100m / totalDays, 1), 0m, 100m);

        var capitalization = version?.Capitalization ?? CapitalizationMode.None;
        var incomeBase = contract.Principal + (capitalization == CapitalizationMode.Monthly ? contract.AccruedInterest : 0m);
        var expectedIncome = contract.IsActive && daysLeft > 0
            ? InterestCalculator.AccrueFlat(incomeBase, rate, daysLeft, InterestCalculator.YearDays(today))
            : 0m;

        var requiresConsent = version?.RequiresBankConsentForEarlyReturn == true && !contract.HasEarlyReturnConsent;

        return new ClientDepositDto
        {
            ContractId = contract.Id,
            ContractNumber = contract.Number,
            AccountNumber = contract.Account?.CurrentNumber(today),
            ProductName = version?.Product?.Name ?? "Вклад",
            ProductDescription = version?.Product?.Description,
            Currency = currency,
            Principal = contract.Principal,
            AccruedInterest = contract.AccruedInterest,
            PaidInterest = contract.PaidInterest,
            RatePercent = rate,
            RateTypeText = version == null ? "фиксированная" : version.RateType.Text(),
            TermMonths = version?.TermMonths ?? 0,
            TermDays = termDays,
            OpenDate = contract.OpenDate,
            EndDate = contract.EndDate == DateOnly.MaxValue ? contract.OpenDate : contract.EndDate,
            DaysLeft = daysLeft,
            ProgressPercent = progress,
            Status = contract.Status.ToString(),
            StatusText = contract.Status.DisplayName(),
            IsActive = contract.IsActive,
            IsRevocable = version?.IsRevocable ?? true,
            AllowsProlongation = version?.AllowsProlongation ?? false,
            CapitalizationText = version == null ? string.Empty : version.Capitalization.Text(),
            InterestPayoutText = version == null ? string.Empty : version.InterestPayout.Text(),
            NextInterestDate = NextInterestDate(contract, today),
            ExpectedIncome = Money.Round(expectedIncome),
            ExpectedTotal = Money.Round(contract.Principal + contract.AccruedInterest + expectedIncome),
            CanEarlyReturn = contract.IsActive && (version?.IsRevocable != false || contract.HasEarlyReturnConsent),
            EarlyReturnNote = requiresConsent
                ? "Безотзывный вклад: досрочный возврат возможен только с согласия банка."
                : "Досрочный возврат возможен с перерасчётом процентов по ставке до востребования."
        };
    }

    private static DateOnly? NextInterestDate(Contract contract, DateOnly today)
    {
        var version = contract.ProductVersion;
        if (version == null || !contract.IsActive)
        {
            return null;
        }

        var monthly = version.InterestPayout == InterestPayoutMode.Monthly
                      || version.Capitalization == CapitalizationMode.Monthly;

        if (!monthly || contract.EndDate == DateOnly.MaxValue)
        {
            return null;
        }

        for (var i = 1; i <= 36; i++)
        {
            var date = SchedulePolicy.AddMonthsClamped(contract.OpenDate, i);
            if (date > today && date <= contract.EndDate)
            {
                return date;
            }
        }

        return null;
    }

    private ClientPortalProfileDto ToProfile(Client client) => new()
    {
        Id = client.Id,
        FullName = client.Name,
        DocumentNumber = client.IdNumber,
        DocumentNumberMasked = MaskDocument(client.IdNumber),
        BirthDate = client.BirthDate,
        PhoneMasked = MaskPhone(client.Phone),
        Email = client.Email,
        IsIdentified = client.IsIdentified,
        IdentificationStatusText = client.IdentificationStatus.Text(),
        Greeting = "Здравствуйте, " + GivenName(client.Name) + "!",
        ServerTime = _clock.UtcNow
    };

    private static string GivenName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return "уважаемый клиент";
        }

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 ? parts[1] + " " + parts[2] : parts[^1];
    }

    private static string NormalizeDocument(string value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("\u00A0", string.Empty);

    private static string MaskDocument(string document)
    {
        if (string.IsNullOrWhiteSpace(document))
        {
            return string.Empty;
        }

        var value = document.Trim();
        if (value.Length <= 4)
        {
            return new string('*', value.Length);
        }

        return value.Substring(0, 2) + new string('*', value.Length - 4) + value.Substring(value.Length - 2);
    }

    private static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 4)
        {
            return "***";
        }

        return "+375 (**) ***-**-" + digits.Substring(digits.Length - 2);
    }
}
