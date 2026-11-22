using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Application.Mapping;
using FinFlow.Contracts;
using FinFlow.Domain.Entities;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>Ведение клиентов банка: регистрация, поиск, идентификация, изменение контактных данных.</summary>
public class ClientService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;
    private readonly IAuditWriter _audit;
    private readonly IBalanceService _balance;
    private readonly ICurrentUser _current;

    public ClientService(IBankDbContext db, IClock clock, IAuditWriter audit, IBalanceService balance, ICurrentUser current)
    {
        _db = db;
        _clock = clock;
        _audit = audit;
        _balance = balance;
        _current = current;
    }

    public async Task<PagedResult<ClientListItemDto>> ListAsync(string query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var source = _db.Clients
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

        return client.ToDetails(accounts);
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

        if (type == ClientType.Individual && !request.BirthDate.HasValue)
        {
            throw new AppException("Для физического лица укажите дату рождения: она используется для входа в личный кабинет.");
        }

        var client = new Client
        {
            Type = type,
            Name = request.Name.Trim(),
            IdNumber = idNumber,
            BirthDate = request.BirthDate,
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

        _db.Clients.Add(client);
        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ClientCreated", nameof(Client), client.Id.ToString(), null,
            new { client.Name, client.IdNumber, client.IdentificationStatus }, cancellationToken);

        return await GetAsync(client.Id, cancellationToken);
    }

    public async Task<ClientDetailsDto> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new AppException("Не переданы данные клиента.");
        }

        var client = await LoadClientAsync(id, cancellationToken);
        var old = new { client.Name, client.Phone, client.Email, client.Notes, client.IdentificationStatus };

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            client.Name = request.Name.Trim();
        }

        if (request.BirthDate.HasValue)
        {
            client.BirthDate = request.BirthDate;
        }

        client.Phone = request.Phone ?? client.Phone;
        client.Email = request.Email ?? client.Email;
        client.Notes = request.Notes ?? client.Notes;

        if (request.Identify == true && client.IdentificationStatus != IdentificationStatus.Identified)
        {
            client.IdentificationStatus = IdentificationStatus.Identified;
            client.IdentifiedAt = _clock.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _audit.WriteAsync("ClientUpdated", nameof(Client), client.Id.ToString(), old,
            new { client.Name, client.Phone, client.Email, client.Notes, client.IdentificationStatus }, cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    private async Task<Client> LoadClientAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await _db.Clients
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
