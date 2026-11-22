using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using FinFlow.Contracts;
using FinFlow.Desktop.Infrastructure;

namespace FinFlow.Desktop.ViewModels;

/// <summary>Отчёт руководителя о работе сотрудников за выбранный период.</summary>
public class ReportsViewModel : ViewModelBase
{
    private readonly Session _session;
    private DateTime? _fromDate = DateTime.Today.AddDays(-29);
    private DateTime? _toDate = DateTime.Today;
    private StaffReportDto _report;

    public ReportsViewModel(Session session)
    {
        _session = session;
        Rows = new ObservableCollection<StaffReportRowDto>();

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        LastMonthCommand = new RelayCommand(() =>
        {
            FromDate = DateTime.Today.AddMonths(-1);
            ToDate = DateTime.Today;
        });
        TodayCommand = new RelayCommand(() =>
        {
            FromDate = DateTime.Today;
            ToDate = DateTime.Today;
        });
    }

    public ObservableCollection<StaffReportRowDto> Rows { get; }

    public DateTime? FromDate
    {
        get => _fromDate;
        set => Set(ref _fromDate, value);
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set => Set(ref _toDate, value);
    }

    public StaffReportDto Report
    {
        get => _report;
        private set
        {
            if (Set(ref _report, value))
            {
                OnPropertyChanged(nameof(PeriodCaption));
                OnPropertyChanged(nameof(TotalsCaption));
            }
        }
    }

    public string PeriodCaption => Report == null
        ? "Отчёт не сформирован."
        : $"Период: {Report.From:dd.MM.yyyy} – {Report.To:dd.MM.yyyy} · сформирован {Report.GeneratedAt:dd.MM.yyyy HH:mm}";

    public string TotalsCaption => Report?.Totals == null
        ? string.Empty
        : $"Сотрудников: {Report.Totals.Employees} · клиентов: {Report.Totals.ClientsRegistered} · "
          + $"счетов: {Report.Totals.AccountsOpened} · вкладов: {Report.Totals.DepositsOpened} на сумму {Report.Totals.DepositAmount:N2} р. · "
          + $"операций: {Report.Totals.OperationsCount} на сумму {Report.Totals.OperationsAmount:N2} р. · закрытий дня: {Report.Totals.DayCloses}";

    public ICommand LoadCommand { get; }
    public ICommand LastMonthCommand { get; }
    public ICommand TodayCommand { get; }

    public async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        if (!FromDate.HasValue || !ToDate.HasValue)
        {
            StatusMessage = "Укажите период отчёта.";
            return;
        }

        if (FromDate.Value.Date > ToDate.Value.Date)
        {
            StatusMessage = "Дата начала периода не может быть позже даты окончания.";
            return;
        }

        var from = DateOnly.FromDateTime(FromDate.Value);
        var to = DateOnly.FromDateTime(ToDate.Value);

        await RunAsync(async () =>
        {
            var report = await _session.Api.GetAsync<StaffReportDto>(
                $"api/reports/staff?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

            Report = report;
            Rows.Clear();
            foreach (var row in report.Rows)
            {
                Rows.Add(row);
            }

            StatusMessage = report.Rows.Count == 0
                ? "За выбранный период данных о работе сотрудников нет."
                : null;
        });
    }
}
