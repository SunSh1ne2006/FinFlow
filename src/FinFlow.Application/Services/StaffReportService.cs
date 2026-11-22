using FinFlow.Application.Abstractions;
using FinFlow.Application.Common;
using FinFlow.Contracts;
using FinFlow.Domain.Calculators;
using FinFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinFlow.Application.Services;

/// <summary>
/// Отчёт руководителя о работе сотрудников за период: зарегистрированные клиенты,
/// открытые счета и вклады, проведённые операции и закрытия операционного дня.
/// </summary>
public class StaffReportService
{
    private readonly IBankDbContext _db;
    private readonly IClock _clock;

    public StaffReportService(IBankDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<StaffReportDto> BuildAsync(DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var actualTo = to ?? _clock.Today;
        var actualFrom = from ?? actualTo.AddDays(-29);

        if (actualFrom > actualTo)
        {
            throw new AppException("Дата начала периода не может быть позже даты окончания.");
        }

        var start = actualFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = actualTo.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var employees = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Where(u => u.UserRoles.Any(ur => ur.Role.Code == RoleCode.Employee))
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        var employeeIds = employees.Select(u => u.Id).ToList();

        var clients = await _db.Clients
            .Where(c => c.CreatedBy != null
                        && employeeIds.Contains(c.CreatedBy.Value)
                        && c.CreatedAt >= start && c.CreatedAt < end)
            .Select(c => new { UserId = c.CreatedBy.Value })
            .ToListAsync(cancellationToken);

        var accounts = await _db.Accounts
            .Where(a => a.CreatedBy != null
                        && employeeIds.Contains(a.CreatedBy.Value)
                        && a.CreatedAt >= start && a.CreatedAt < end)
            .Select(a => new { UserId = a.CreatedBy.Value })
            .ToListAsync(cancellationToken);

        var deposits = await _db.Contracts
            .Where(c => c.CreatedBy != null
                        && employeeIds.Contains(c.CreatedBy.Value)
                        && c.CreatedAt >= start && c.CreatedAt < end
                        && c.ProductVersion.Product.Kind == ProductKind.Deposit)
            .Select(c => new { UserId = c.CreatedBy.Value, c.Principal })
            .ToListAsync(cancellationToken);

        var operations = await _db.Operations
            .Where(o => o.CreatedBy != null
                        && employeeIds.Contains(o.CreatedBy.Value)
                        && o.CreatedAt >= start && o.CreatedAt < end)
            .Select(o => new { UserId = o.CreatedBy.Value, o.Amount })
            .ToListAsync(cancellationToken);

        var dayCloses = await _db.AuditRecords
            .Where(a => a.Action == "DayClosed"
                        && a.UserId != null
                        && employeeIds.Contains(a.UserId.Value)
                        && a.At >= start && a.At < end)
            .Select(a => new { UserId = a.UserId.Value })
            .ToListAsync(cancellationToken);

        var records = employees.Select(user =>
        {
            var userDeposits = deposits.Where(d => d.UserId == user.Id).ToList();
            var userOperations = operations.Where(o => o.UserId == user.Id).ToList();
            var role = user.UserRoles.FirstOrDefault()?.Role;

            return new StaffWorkRecord
            {
                UserId = user.Id,
                Login = user.Login,
                FullName = user.FullName,
                Role = role?.Name ?? "Сотрудник банка",
                ClientsRegistered = clients.Count(c => c.UserId == user.Id),
                AccountsOpened = accounts.Count(a => a.UserId == user.Id),
                DepositsOpened = userDeposits.Count,
                DepositAmount = userDeposits.Sum(d => d.Principal),
                OperationsCount = userOperations.Count,
                OperationsAmount = userOperations.Sum(o => o.Amount),
                DayCloses = dayCloses.Count(d => d.UserId == user.Id)
            };
        }).ToList();

        var totals = StaffWorkCalculator.Summarize(records);

        return new StaffReportDto
        {
            From = actualFrom,
            To = actualTo,
            GeneratedAt = _clock.UtcNow,
            Totals = new StaffReportTotalsDto
            {
                Employees = totals.Employees,
                ClientsRegistered = totals.ClientsRegistered,
                AccountsOpened = totals.AccountsOpened,
                DepositsOpened = totals.DepositsOpened,
                DepositAmount = totals.DepositAmount,
                OperationsCount = totals.OperationsCount,
                OperationsAmount = totals.OperationsAmount,
                DayCloses = totals.DayCloses
            },
            Rows = records.Select(r => new StaffReportRowDto
            {
                UserId = r.UserId,
                Login = r.Login,
                FullName = r.FullName,
                Role = r.Role,
                ClientsRegistered = r.ClientsRegistered,
                AccountsOpened = r.AccountsOpened,
                DepositsOpened = r.DepositsOpened,
                DepositAmount = r.DepositAmount,
                OperationsCount = r.OperationsCount,
                OperationsAmount = r.OperationsAmount,
                DayCloses = r.DayCloses
            }).ToList()
        };
    }
}
