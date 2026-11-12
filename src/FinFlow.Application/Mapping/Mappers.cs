using FinFlow.Application.Common;
using FinFlow.Contracts;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;

namespace FinFlow.Application.Mapping;

/// <summary>Преобразование доменных сущностей в объекты передачи данных (DTO).</summary>
public static class Mappers
{
    public static UserDto ToDto(this User user)
    {
        if (user == null)
        {
            return null;
        }

        var roles = user.UserRoles?.Where(ur => ur.Role != null).ToList() ?? new List<UserRole>();
        return new UserDto
        {
            Id = user.Id,
            Login = user.Login,
            FullName = user.FullName,
            Roles = roles.Select(ur => ur.Role.Code.ToString()).ToList(),
            RoleNames = roles.Select(ur => ur.Role.Name).ToList(),
            TwoFactorEnabled = user.TwoFactorEnabled,
            LastLoginAt = user.LastLoginAt,
            IsBlocked = user.IsBlocked
        };
    }

    public static KycProfileDto ToDto(this KycProfile kyc, DateOnly today)
    {
        if (kyc == null)
        {
            return null;
        }

        return new KycProfileDto
        {
            Id = kyc.Id,
            RiskLevel = kyc.RiskLevel.ToString(),
            ReviewDate = kyc.ReviewDate,
            Source = kyc.Source,
            Notes = kyc.Notes,
            IsActual = kyc.IsActual,
            IsOverdue = kyc.IsOverdue(today),
            UpdatedAt = kyc.UpdatedAt
        };
    }

    public static RepresentativeDto ToDto(this Representative representative, DateOnly today)
    {
        return new RepresentativeDto
        {
            Id = representative.Id,
            FullName = representative.FullName,
            IdNumber = representative.IdNumber,
            Authority = representative.Authority,
            ValidFrom = representative.ValidFrom,
            ValidTo = representative.ValidTo,
            IsValid = representative.IsValidOn(today)
        };
    }

    public static ClientListItemDto ToListItem(this Client client)
    {
        return new ClientListItemDto
        {
            Id = client.Id,
            Name = client.Name,
            Type = client.Type.ToString(),
            TypeText = client.Type.Text(),
            IdNumber = client.IdNumber,
            IdentificationStatus = client.IdentificationStatus.ToString(),
            IsIdentified = client.IsIdentified,
            KycRiskLevel = client.KycProfile?.RiskLevel.ToString(),
            AccountsCount = client.Accounts?.Count ?? 0,
            CreatedAt = client.CreatedAt
        };
    }

    public static ClientDetailsDto ToDetails(this Client client, DateOnly today, IReadOnlyList<AccountListItemDto> accounts)
    {
        var listItem = client.ToListItem();
        return new ClientDetailsDto
        {
            Id = listItem.Id,
            Name = listItem.Name,
            Type = listItem.Type,
            TypeText = listItem.TypeText,
            IdNumber = listItem.IdNumber,
            IdentificationStatus = listItem.IdentificationStatus,
            IsIdentified = listItem.IsIdentified,
            KycRiskLevel = listItem.KycRiskLevel,
            AccountsCount = accounts?.Count ?? listItem.AccountsCount,
            CreatedAt = listItem.CreatedAt,
            Phone = client.Phone,
            Email = client.Email,
            Country = client.Country,
            IsResident = client.IsResident,
            IdentifiedAt = client.IdentifiedAt,
            Notes = client.Notes,
            Kyc = client.KycProfile.ToDto(today),
            Representatives = client.Representatives?.Select(r => r.ToDto(today)).ToList() ?? new List<RepresentativeDto>(),
            Accounts = accounts ?? Array.Empty<AccountListItemDto>()
        };
    }

    public static AccountListItemDto ToListItem(this Account account, DateOnly today, decimal availableBalance, Contract contract = null)
    {
        var numbers = account.Numbers ?? new List<AccountNumber>();
        var current = numbers
            .Where(n => n.ValidFrom <= today && (n.ValidTo == null || n.ValidTo >= today))
            .OrderByDescending(n => n.ValidFrom)
            .FirstOrDefault() ?? numbers.FirstOrDefault();

        return new AccountListItemDto
        {
            Id = account.Id,
            ClientId = account.ClientId,
            ClientName = account.Client?.Name,
            Number = current?.Number,
            PreviousNumber = numbers.Where(n => n.Kind == NumberKind.Previous).Select(n => n.Number).FirstOrDefault(),
            ModifiedNumber = numbers.Where(n => n.Kind == NumberKind.Modified).Select(n => n.Number).FirstOrDefault(),
            Currency = account.Currency,
            BalanceAccount = account.BalanceAccount,
            Balance = account.Balance,
            OverdraftLimit = account.OverdraftLimit,
            Status = account.Status.ToString(),
            ContractNumber = contract?.Number,
            ProductName = contract?.ProductVersion?.Product?.Name,
            AvailableBalance = availableBalance
        };
    }

    public static AccountDetailsDto ToDetails(
        this Account account,
        BalanceSnapshotDto snapshot,
        Contract contract,
        IReadOnlyList<OperationDto> recentOperations)
    {
        return new AccountDetailsDto
        {
            Id = account.Id,
            ClientId = account.ClientId,
            ClientName = account.Client?.Name,
            Number = snapshot.AccountNumber,
            PreviousNumber = account.Numbers?.Where(n => n.Kind == NumberKind.Previous).Select(n => n.Number).FirstOrDefault(),
            ModifiedNumber = account.Numbers?.Where(n => n.Kind == NumberKind.Modified).Select(n => n.Number).FirstOrDefault(),
            Currency = account.Currency,
            BalanceAccount = account.BalanceAccount,
            Balance = account.Balance,
            OverdraftLimit = account.OverdraftLimit,
            Status = account.Status.ToString(),
            ContractNumber = contract?.Number,
            ProductName = contract?.ProductVersion?.Product?.Name,
            AvailableBalance = snapshot.AvailableBalance,
            HoldsAmount = snapshot.HoldsAmount,
            HasArrest = snapshot.HasArrest,
            AvailableBalanceCalculated = snapshot.AvailableBalance,
            OverdraftUsed = snapshot.OverdraftUsed,
            OverdraftUtilizationPercent = snapshot.OverdraftUtilizationPercent,
            Numbers = account.Numbers?.Select(n => new AccountNumberDto
            {
                Id = n.Id,
                Number = n.Number,
                Kind = n.Kind.ToString(),
                ValidFrom = n.ValidFrom,
                ValidTo = n.ValidTo
            }).ToList() ?? new List<AccountNumberDto>(),
            Holds = account.Holds?.Select(h => h.ToDto()).ToList() ?? new List<HoldDto>(),
            Contract = contract?.ToDto(),
            RecentOperations = recentOperations ?? Array.Empty<OperationDto>()
        };
    }

    public static HoldDto ToDto(this Hold hold)
    {
        return new HoldDto
        {
            Id = hold.Id,
            AccountId = hold.AccountId,
            Source = hold.Source.ToString(),
            SourceText = hold.Source.Text(),
            BasisNumber = hold.BasisNumber,
            BasisAuthority = hold.BasisAuthority,
            Amount = hold.Amount,
            IsArrest = hold.IsArrest,
            ValidFrom = hold.ValidFrom,
            ValidTo = hold.ValidTo,
            Status = hold.Status.ToString(),
            Comment = hold.Comment,
            CreatedAt = hold.CreatedAt
        };
    }

    public static ProductDto ToDto(this Product product, DateOnly today)
    {
        return new ProductDto
        {
            Id = product.Id,
            Code = product.Code,
            Name = product.Name,
            Kind = product.Kind.ToString(),
            KindText = product.Kind.Text(),
            Currency = product.Currency,
            BalanceAccount = product.BalanceAccount,
            IsActive = product.IsActive,
            Description = product.Description,
            Versions = product.Versions?.OrderByDescending(v => v.Version).Select(v => v.ToDto(today)).ToList() ?? new List<ProductVersionDto>()
        };
    }

    public static ProductVersionDto ToDto(this ProductVersion version, DateOnly today)
    {
        return new ProductVersionDto
        {
            Id = version.Id,
            ProductId = version.ProductId,
            Version = version.Version,
            RatePercent = version.RatePercent,
            RateType = version.RateType.ToString(),
            SpreadPercent = version.SpreadPercent,
            DepositType = version.DepositType.ToString(),
            DepositTypeText = version.DepositType.Text(),
            IsRevocable = version.IsRevocable,
            AllowsProlongation = version.AllowsProlongation,
            TermMonths = version.TermMonths,
            Capitalization = version.Capitalization.ToString(),
            InterestPayout = version.InterestPayout.ToString(),
            MinAmount = version.MinAmount,
            MinBalance = version.MinBalance,
            DayBase = version.DayBase,
            ValidFrom = version.ValidFrom,
            IsApproved = version.IsApproved,
            ApprovedAt = version.ApprovedAt,
            RvsrLimit = version.RvsrLimit,
            RequiresBankConsentForEarlyReturn = version.RequiresBankConsentForEarlyReturn,
            Notes = version.Notes
        };
    }

    public static ContractDto ToDto(this Contract contract)
    {
        if (contract == null)
        {
            return null;
        }

        return new ContractDto
        {
            Id = contract.Id,
            Number = contract.Number,
            ClientId = contract.ClientId,
            ClientName = contract.Client?.Name,
            AccountId = contract.AccountId,
            AccountNumber = contract.Account?.Numbers
                ?.Where(n => n.ValidTo == null || n.ValidTo >= contract.OpenDate)
                .OrderByDescending(n => n.ValidFrom)
                .Select(n => n.Number)
                .FirstOrDefault(),
            ProductVersionId = contract.ProductVersionId,
            ProductName = contract.ProductVersion?.Product?.Name,
            OpenDate = contract.OpenDate,
            EndDate = contract.EndDate,
            Principal = contract.Principal,
            AccruedInterest = contract.AccruedInterest,
            PaidInterest = contract.PaidInterest,
            Status = contract.Status.ToString(),
            StatusText = contract.Status.DisplayName(),
            RatePercent = contract.ProductVersion?.RatePercent ?? 0m,
            DepositType = contract.ProductVersion?.DepositType.ToString(),
            IsRevocable = contract.ProductVersion?.IsRevocable ?? true,
            MinBalance = contract.ProductVersion?.MinBalance ?? 0m,
            TermMonths = contract.ProductVersion?.TermMonths ?? 0,
            ProlongationCount = contract.ProlongationCount,
            TermDays = contract.TermDays,
            HasEarlyReturnConsent = contract.HasEarlyReturnConsent,
            Currency = contract.Account?.Currency
        };
    }

    public static InterestAccrualDto ToDto(this InterestAccrual accrual)
    {
        return new InterestAccrualDto
        {
            Id = accrual.Id,
            ContractId = accrual.ContractId,
            AccrualDate = accrual.AccrualDate,
            Base = accrual.Base,
            RatePercent = accrual.RatePercent,
            YearDays = accrual.YearDays,
            Amount = accrual.Amount,
            Status = accrual.Status.ToString()
        };
    }

    public static OperationDto ToDto(this Operation operation, IReadOnlyDictionary<Guid, string> logins = null)
    {
        if (operation == null)
        {
            return null;
        }

        var currentNumber = operation.Account?.Numbers
            ?.OrderByDescending(n => n.ValidFrom)
            .Select(n => n.Number)
            .FirstOrDefault();

        return new OperationDto
        {
            Id = operation.Id,
            AccountId = operation.AccountId,
            AccountNumber = currentNumber,
            ClientName = operation.Account?.Client?.Name,
            ContractId = operation.ContractId,
            ContractNumber = operation.Contract?.Number,
            DocumentId = operation.DocumentId,
            DocumentNumber = operation.Document?.Number,
            Kind = operation.Kind.ToString(),
            KindText = operation.Kind.DisplayName(),
            Amount = operation.Amount,
            Currency = operation.Currency,
            Status = operation.Status.ToString(),
            StatusText = operation.Status.DisplayName(),
            Description = operation.Description,
            CreatedAt = operation.CreatedAt,
            CreatedBy = operation.CreatedBy,
            CreatedByLogin = LoginOf(logins, operation.CreatedBy),
            ApprovedBy = operation.ApprovedBy,
            ApprovedByLogin = LoginOf(logins, operation.ApprovedBy),
            ApprovedAt = operation.ApprovedAt,
            ExecutedAt = operation.ExecutedAt,
            RejectionReason = operation.RejectionReason,
            Signature = operation.Signature,
            CheckedBalance = operation.CheckedBalance,
            CheckedOverdraft = operation.CheckedOverdraft,
            CheckedHolds = operation.CheckedHolds,
            CheckedAvailable = operation.CheckedAvailable,
            CanBeApproved = operation.Status == OperationStatus.OnControl
        };
    }

    public static RiskDto ToDto(this Risk risk, DateOnly today)
    {
        var assessment = risk.LatestAssessment;
        var probability = assessment?.Probability ?? 0m;
        var impact = assessment?.MoneyImpact ?? 0m;
        var score = RiskCalculator.Evaluate(new RiskInput
        {
            Code = risk.Code,
            Probability = probability,
            MoneyImpact = impact,
            MitigatedProbability = assessment?.MitigatedProbability
        });

        return new RiskDto
        {
            Id = risk.Id,
            Code = risk.Code,
            Event = risk.Event,
            Cause = risk.Cause,
            Consequence = risk.Consequence,
            Category = risk.Category,
            OwnerRole = risk.OwnerRole,
            Status = risk.Status.ToString(),
            Mitigation = risk.Mitigation,
            EarlyIndicator = risk.EarlyIndicator,
            Probability = probability,
            MoneyImpact = impact,
            MitigatedProbability = assessment?.MitigatedProbability,
            ProbabilityScore = score.ProbabilityScore,
            ImpactScore = score.ImpactScore,
            Score = score.Score,
            Zone = score.Zone.ToString(),
            ZoneText = score.Zone.DisplayName(),
            Emv = score.Emv,
            ResidualEmv = score.ResidualEmv,
            AssessmentDate = assessment?.Date
        };
    }

    public static ParameterDto ToDto(this Parameter parameter)
    {
        return new ParameterDto
        {
            Id = parameter.Id,
            Code = parameter.Code,
            Name = parameter.Name,
            Unit = parameter.Unit,
            Value = parameter.Value,
            TextValue = parameter.TextValue,
            ValidFrom = parameter.ValidFrom,
            IsActive = parameter.IsActive
        };
    }

    public static AuditRecordDto ToDto(this AuditRecord record)
    {
        return new AuditRecordDto
        {
            Id = record.Id,
            At = record.At,
            UserLogin = record.UserLogin,
            Action = record.Action,
            EntityName = record.EntityName,
            EntityId = record.EntityId,
            OldValue = record.OldValue,
            NewValue = record.NewValue
        };
    }

    public static OperationalDayDto ToDto(this OperationalDay day)
    {
        return new OperationalDayDto
        {
            Id = day.Id,
            Date = day.Date,
            Status = day.Status.ToString(),
            Stage = day.Stage.ToString(),
            StageText = day.Stage.Text(),
            StartedAt = day.StartedAt,
            ClosedAt = day.ClosedAt,
            AccrualCount = day.AccrualCount,
            PostingCount = day.PostingCount,
            RejectedOperations = day.RejectedOperations,
            InterestAccrued = day.InterestAccrued,
            TaxWithheld = day.TaxWithheld,
            LastError = day.LastError
        };
    }

    private static string LoginOf(IReadOnlyDictionary<Guid, string> logins, Guid? userId)
    {
        if (logins == null || !userId.HasValue)
        {
            return null;
        }

        return logins.TryGetValue(userId.Value, out var login) ? login : null;
    }
}
