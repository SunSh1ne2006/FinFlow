using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using FinFlow.Contracts;
using FinFlow.Desktop.Infrastructure;

namespace FinFlow.Desktop.ViewModels;

/// <summary>Закрытие операционного дня и сверка (требования 10, 11, 19).</summary>
public class DayCloseViewModel : ViewModelBase
{
    private readonly Session _session;
    private OperationalDayDto _today;
    private ReconciliationDto _reconciliation;

    public DayCloseViewModel(Session session)
    {
        _session = session;
        History = new ObservableCollection<OperationalDayDto>();

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        StartCommand = new AsyncRelayCommand(StartAsync, () => _today == null || _today.Status != "Closed");
        ContinueCommand = new AsyncRelayCommand(ContinueAsync, () => _today != null && _today.Status != "Closed");
        ReconcileCommand = new AsyncRelayCommand(ReconcileAsync);
    }

    public ObservableCollection<OperationalDayDto> History { get; }

    public OperationalDayDto Today
    {
        get => _today;
        private set
        {
            if (Set(ref _today, value))
            {
                OnPropertyChanged(nameof(TodayCaption));
                OnPropertyChanged(nameof(TotalsCaption));
                OnPropertyChanged(nameof(ErrorCaption));
            }
        }
    }

    public ReconciliationDto Reconciliation
    {
        get => _reconciliation;
        private set
        {
            if (Set(ref _reconciliation, value))
            {
                OnPropertyChanged(nameof(ReconciliationCaption));
            }
        }
    }

    public string TodayCaption => Today == null
        ? "Операционный день не открыт."
        : $"Операционный день {Today.Date:dd.MM.yyyy} · {Today.Status} · этап: {Today.StageText}";

    public string TotalsCaption => Today == null
        ? string.Empty
        : $"Начислено процентов: {Today.InterestAccrued:N2} р. · удержано налога: {Today.TaxWithheld:N2} р. · начислений: {Today.AccrualCount} · проводок: {Today.PostingCount} · отклонено операций: {Today.RejectedOperations}";

    public string ErrorCaption => string.IsNullOrWhiteSpace(Today?.LastError)
        ? string.Empty
        : "Последняя ошибка: " + Today.LastError;

    public string ReconciliationCaption => Reconciliation == null
        ? "Сверка не выполнялась."
        : $"Сверка {Reconciliation.Date:dd.MM.yyyy}: дебет {Reconciliation.DebitTotal:N2}, кредит {Reconciliation.CreditTotal:N2}, расхождение {Reconciliation.Difference:N2}. {Reconciliation.Message}";

    public ICommand LoadCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand ContinueCommand { get; }
    public ICommand ReconcileCommand { get; }

    public async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await RunAsync(FetchAsync);
    }

    private async Task FetchAsync()
    {
        Today = await _session.Api.GetAsync<OperationalDayDto>("api/dayclose/current");

        var history = await _session.Api.GetAsync<System.Collections.Generic.List<OperationalDayDto>>("api/dayclose/history?take=30");
        History.Clear();
        foreach (var day in history)
        {
            History.Add(day);
        }
    }

    private async Task StartAsync()
    {
        await RunAsync(async () =>
        {
            await _session.Api.PostAsync<OperationalDayDto>("api/dayclose/start");
            await FetchAsync();
            StatusMessage = "Закрытие дня запущено: отклонение неподтверждённых операций.";
        });
    }

    private async Task ContinueAsync()
    {
        await RunAsync(async () =>
        {
            await _session.Api.PostAsync<OperationalDayDto>("api/dayclose/continue");
            await FetchAsync();
            StatusMessage = "Следующий этап закрытия дня выполнен.";
        });
    }

    private async Task ReconcileAsync()
    {
        await RunAsync(async () =>
        {
            Reconciliation = await _session.Api.GetAsync<ReconciliationDto>("api/dayclose/reconciliation");
            StatusMessage = Reconciliation?.IsBalanced == true ? "Сверка успешна: расхождений нет." : "Сверка выявила расхождение.";
        });
    }
}
