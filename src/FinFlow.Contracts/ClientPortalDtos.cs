namespace FinFlow.Contracts;

/// <summary>
/// Вход в личный кабинет клиента по паспортным данным: серия и номер документа
/// и дата рождения (оба реквизита хранятся в базе банка).
/// </summary>
public class ClientLoginRequest
{
    public string DocumentNumber { get; set; }
    public DateOnly? BirthDate { get; set; }
}

public class ClientLoginResponse
{
    public string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public ClientPortalProfileDto Client { get; set; }
    /// <summary>Дата рождения была зафиксирована при первом входе (первичная активация кабинета).</summary>
    public bool BirthDateRegistered { get; set; }
}

public class ClientPortalProfileDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; }
    public string DocumentType { get; set; } = "Паспорт";
    public string DocumentNumber { get; set; }
    public string DocumentNumberMasked { get; set; }
    public DateOnly? BirthDate { get; set; }
    public string PhoneMasked { get; set; }
    public string Email { get; set; }
    public bool IsIdentified { get; set; }
    public string IdentificationStatusText { get; set; }
    public string Greeting { get; set; }
    public DateTime ServerTime { get; set; }
}

/// <summary>Суммарные показатели по валютам для главного экрана кабинета.</summary>
public class ClientCurrencyTotalDto
{
    public string Currency { get; set; }
    public decimal Principal { get; set; }
    public decimal AccruedInterest { get; set; }
    public decimal ExpectedIncome { get; set; }
    public int DepositsCount { get; set; }
}

public class ClientDepositDto
{
    public Guid ContractId { get; set; }
    public string ContractNumber { get; set; }
    public string AccountNumber { get; set; }
    public string ProductName { get; set; }
    public string ProductDescription { get; set; }
    public string Currency { get; set; }
    public decimal Principal { get; set; }
    public decimal AccruedInterest { get; set; }
    public decimal PaidInterest { get; set; }
    public decimal RatePercent { get; set; }
    public string RateTypeText { get; set; }
    public int TermMonths { get; set; }
    public int TermDays { get; set; }
    public DateOnly OpenDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int DaysLeft { get; set; }
    public decimal ProgressPercent { get; set; }
    public string Status { get; set; }
    public string StatusText { get; set; }
    public bool IsActive { get; set; }
    public bool IsRevocable { get; set; }
    public bool AllowsProlongation { get; set; }
    public string CapitalizationText { get; set; }
    public string InterestPayoutText { get; set; }
    public DateOnly? NextInterestDate { get; set; }
    public decimal ExpectedIncome { get; set; }
    public decimal ExpectedTotal { get; set; }
    public bool CanEarlyReturn { get; set; }
    public string EarlyReturnNote { get; set; }
}

public class ClientDepositsDto
{
    public IReadOnlyList<ClientDepositDto> Deposits { get; set; } = Array.Empty<ClientDepositDto>();
    public IReadOnlyList<ClientCurrencyTotalDto> Totals { get; set; } = Array.Empty<ClientCurrencyTotalDto>();
    public int ActiveCount { get; set; }
    public int ClosedCount { get; set; }
    public DateOnly AsOf { get; set; }
}

public class ClientAccrualDto
{
    public DateOnly Date { get; set; }
    public decimal Base { get; set; }
    public decimal RatePercent { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; }
}

/// <summary>Оценка выплаты при досрочном возврате вклада сегодня (формулы 1.9–1.10).</summary>
public class ClientEarlyReturnEstimateDto
{
    public bool Available { get; set; }
    public bool RequiresBankConsent { get; set; }
    public bool ConsentGranted { get; set; }
    public int Days { get; set; }
    public decimal Payout { get; set; }
    public decimal ContractInterest { get; set; }
    public decimal DemandInterest { get; set; }
    public decimal Withheld { get; set; }
    public string Note { get; set; }
}

public class ClientDepositDetailsDto : ClientDepositDto
{
    public decimal Balance { get; set; }
    public IReadOnlyList<string> AccountNumbers { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ClientAccrualDto> Accruals { get; set; } = Array.Empty<ClientAccrualDto>();
    public ClientEarlyReturnEstimateDto EarlyReturn { get; set; }
    public string TaxNote { get; set; }
}

/// <summary>Условия по сроку для доступного вклада.</summary>
public class ClientOfferTermDto
{
    public Guid VersionId { get; set; }
    public decimal RatePercent { get; set; }
    public int TermMonths { get; set; }
    public int TermDays { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MinBalance { get; set; }
    public bool IsRevocable { get; set; }
    public string InterestPayoutText { get; set; }
    public string CapitalizationText { get; set; }
    public decimal IncomePer1000 { get; set; }
    public bool RateWithinRvsr { get; set; }
}

public class ClientOfferDto
{
    public Guid ProductId { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Currency { get; set; }
    public decimal MinAmount { get; set; }
    public bool IsRevocable { get; set; }
    public bool AllowsProlongation { get; set; }
    public string CapitalizationText { get; set; }
    public string InterestPayoutText { get; set; }
    public decimal BestRate { get; set; }
    public int MinTermMonths { get; set; }
    public int MaxTermMonths { get; set; }
    public string Note { get; set; }
    public IReadOnlyList<ClientOfferTermDto> Terms { get; set; } = Array.Empty<ClientOfferTermDto>();
}

public class ClientOffersDto
{
    public IReadOnlyList<ClientOfferDto> Offers { get; set; } = Array.Empty<ClientOfferDto>();
    public decimal DemandRatePercent { get; set; }
    public decimal RvsrByn1To3Years { get; set; }
    public decimal RvsrBynOver3Years { get; set; }
    public DateOnly AsOf { get; set; }
    public string Note { get; set; }
}
