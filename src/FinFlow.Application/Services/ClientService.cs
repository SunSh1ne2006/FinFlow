using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>Ведение клиентов, анкет ПОД/ФТ и представителей (требование 1).</summary>
public class ClientService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly IBalanceService _balance;
    private readonly ICurrentUser _current;
    private readonly IMsiGateway _msi;

    public ClientService(IBankDbContext db, IClock clock, IAuditWriter audit, IBalanceService balance, ICurrentUser current, IMsiGateway msi)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _balance = balance;
        _current = current;
        _msi = msi;
    }

    public async Task<PagedResult<ClientListItemDto>> ListAsync(string query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var source = _db.Clients
            .Include(c => c.KycProfile)
            .Include(c => c.Accounts)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.Trim();
            source = source.Where(c => c.Name.Contains(search) || c.IdNumber.Contains(search));
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ClientListItemDto>
        {
            Items = items.Select(c => c.ToListItem()).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ClientDetailsDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var client = await LoadClientAsync(id, cancellationToken);
        var accounts = new List<AccountListItemDto>();
        foreach (var account in client.Accounts)
        {
            var available = await _balance.GetAvailableAsync(account, _clock.Today, cancellationToken);
            var contract = account.Contracts?.OrderByDescending(c => c.OpenDate).FirstOrDefault();
            accounts.Add(account.ToListItem(_clock.Today, available, contract));
        }

        return client.ToDetails(_clock.Today, accounts);
    }

    public async Task<ClientDetailsDto> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppException("Укажите наименование (ФИО) клиента.");
        }

        if (string.IsNullOrWhiteSpace(request.IdNumber))
        {
            throw new AppException("Укажите идентификационный номер (УНП или номер документа).");
        }

        var type = Parsing.Enum<ClientType>(request.Type, "тип клиента", ClientType.Individual);
        var idNumber = request.IdNumber.Trim();

        var exists = await _db.Clients.AnyAsync(c => c.Type == type && c.IdNumber == idNumber, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"Клиент с идентификационным номером {idNumber} уже зарегистрирован.");
        }

        var riskLevel = Parsing.Enum<KycRiskLevel>(request.KycRiskLevel, "уровень риска ПОД/ФТ", KycRiskLevel.Low);
        var client = new Client
        {
            Type = type,
            Name = request.Name.Trim(),
            IdNumber = idNumber,
            Phone = request.Phone,
            Email = request.Email,
            Country = string.IsNullOrWhiteSpace(request.Country) ? "BY" : request.Country.Trim(),
            IsResident = request.IsResident,
            IdentificationStatus = IdentificationStatus.Identified,
            IdentifiedAt = _clock.UtcNow,
            Notes = request.Notes,
            CreatedBy = _current.UserId,
            CreatedAt = _clock.UtcNow
        };

        if (request.UseMsi)
        {
            var msiData = await _msi.GetClientDataAsync(idNumber, cancellationToken);
            if (msiData != null && msiData.Found)
            {
                client.Phone = string.IsNullOrWhiteSpace(client.Phone) ? msiData.Phone : client.Phone;
                client.Email = string.IsNullOrWhiteSpace(client.Email) ? msiData.Email : client.Email;
                client.Notes = string.IsNullOrWhiteSpace(client.Notes) ? msiData.Comment : client.Notes;
            }
        }

        client.KycProfile = new KycProfile
        {
            ClientId = client.Id,
            RiskLevel = riskLevel,
            ReviewDate = _clock.Today,
            Source = request.UseMsi ? "МСИ" : "ЦБУ",
            Notes = request.Notes,
            IsActual = true,
            UpdatedAt = _clock.UtcNow
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ClientCreated", nameof(Client), client.Id.ToString(), null,
            new { client.Name, client.IdNumber, client.IdentificationStatus }, cancellationToken);

        return await GetAsync(client.Id, cancellationToken);
    }

    public async Task<ClientDetailsDto> UpdateKycAsync(Guid id, UpdateKycRequest request, CancellationToken cancellationToken = default)
    {
        var client = await LoadClientAsync(id, cancellationToken);
        var kyc = client.KycProfile;
        var old = kyc == null ? null : new { RiskLevel = kyc.RiskLevel.ToString(), kyc.ReviewDate, kyc.Notes };

        if (kyc == null)
        {
            kyc = new KycProfile { ClientId = client.Id, ReviewDate = _clock.Today, IsActual = true };
            client.KycProfile = kyc;
            _db.KycProfiles.Add(kyc);
        }

        if (!string.IsNullOrWhiteSpace(request?.RiskLevel))
        {
            kyc.RiskLevel = Parsing.Enum<KycRiskLevel>(request.RiskLevel, "уровень риска", kyc.RiskLevel);
        }

        kyc.Notes = request?.Notes ?? kyc.Notes;
        kyc.ReviewDate = request?.ReviewDate ?? _clock.Today;
        kyc.IsActual = true;
        kyc.UpdatedAt = _clock.UtcNow;

        if (client.IdentificationStatus == IdentificationStatus.RequiresUpdate)
        {
            client.IdentificationStatus = IdentificationStatus.Identified;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("KycUpdated", nameof(KycProfile), kyc.Id.ToString(), old,
            new { RiskLevel = kyc.RiskLevel.ToString(), kyc.ReviewDate, kyc.Notes }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task<RepresentativeDto> AddRepresentativeAsync(Guid clientId, AddRepresentativeRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new AppException("Укажите ФИО представителя.");
        }

        if (request.ValidTo < request.ValidFrom)
        {
            throw new AppException("Дата окончания доверенности не может быть раньше даты начала.");
        }

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, cancellationToken);
        if (client == null)
        {
            throw new NotFoundException("Клиент не найден.");
        }

        var representative = new Representative
        {
            ClientId = clientId,
            FullName = request.FullName.Trim(),
            IdNumber = request.IdNumber,
            Authority = request.Authority,
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo
        };

        _db.Representatives.Add(representative);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("RepresentativeAdded", nameof(Representative), representative.Id.ToString(), null,
            new { representative.FullName, representative.ValidFrom, representative.ValidTo }, cancellationToken);

        return representative.ToDto(_clock.Today);
    }

    /// <summary>Получение данных клиента из МСИ с его согласия (требование 1, постановление № 497).</summary>
    public async Task<ClientDetailsDto> SyncWithMsiAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var client = await LoadClientAsync(id, cancellationToken);
        var msiData = await _msi.GetClientDataAsync(client.IdNumber, cancellationToken);

        if (msiData == null || !msiData.Found)
        {
            throw new AppException("Данные клиента в МСИ не найдены.");
        }

        client.Phone = string.IsNullOrWhiteSpace(msiData.Phone) ? client.Phone : msiData.Phone;
        client.Email = string.IsNullOrWhiteSpace(msiData.Email) ? client.Email : msiData.Email;
        client.IdentificationStatus = IdentificationStatus.Identified;
        client.IdentifiedAt ??= _clock.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("MsiSync", nameof(Client), client.Id.ToString(), null,
            new { msiData.FullName, msiData.Phone, msiData.Email }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    private async Task<Client> LoadClientAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await _db.Clients
            .Include(c => c.KycProfile)
            .Include(c => c.Representatives)
            .Include(c => c.Accounts)
            .ThenInclude(a => a.Numbers)
            .Include(c => c.Accounts)
            .ThenInclude(a => a.Contracts)
            .ThenInclude(k => k.ProductVersion)
            .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (client == null)
        {
            throw new NotFoundException("Клиент не найден.");
        }

        return client;
    }
}
