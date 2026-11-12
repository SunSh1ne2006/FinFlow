using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using FinFlow.Contracts;
using FinFlow.Desktop.Infrastructure;

namespace FinFlow.Desktop.ViewModels;

/// <summary>Очередь контроля: подтверждение и отклонение операций (UC04, требование 12).</summary>
public class ControlQueueViewModel : ViewModelBase
{
    private readonly Session _session;
    private OperationDto _selectedOperation;
    private string _signatureText;
    private string _reasonText;

    public ControlQueueViewModel(Session session)
    {
        _session = session;
        Queue = new ObservableCollection<OperationDto>();

        LoadCommand = new AsyncRelayCommand(() => LoadAsync(), () => true);
        ApproveCommand = new AsyncRelayCommand(ApproveAsync, () => _selectedOperation != null);
        RejectCommand = new AsyncRelayCommand(RejectAsync, () => _selectedOperation != null);
    }

    public ObservableCollection<OperationDto> Queue { get; }

    public OperationDto SelectedOperation
    {
        get => _selectedOperation;
        set
        {
            if (Set(ref _selectedOperation, value))
            {
                OnPropertyChanged(nameof(SelectedOperationCaption));
                OnPropertyChanged(nameof(CheckedCaption));
            }
        }
    }

    public string SelectedOperationCaption => SelectedOperation == null
        ? "Операция не выбрана."
        : $"{SelectedOperation.KindText} · {SelectedOperation.Amount:N2} {SelectedOperation.Currency} · счёт {SelectedOperation.AccountNumber} · {SelectedOperation.ClientName}";

    public string CheckedCaption => SelectedOperation == null
        ? string.Empty
        : $"Проверено при постановке на контроль: остаток {SelectedOperation.CheckedBalance:N2}, овердрафт {SelectedOperation.CheckedOverdraft:N2}, аресты {SelectedOperation.CheckedHolds:N2}, доступно {SelectedOperation.CheckedAvailable:N2}.";

    public string SignatureText
    {
        get => _signatureText;
        set => Set(ref _signatureText, value);
    }

    public string ReasonText
    {
        get => _reasonText;
        set => Set(ref _reasonText, value);
    }

    public ICommand LoadCommand { get; }
    public ICommand ApproveCommand { get; }
    public ICommand RejectCommand { get; }

    public async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await RunAsync(FetchAsync);
    }

    private async Task FetchAsync()
    {
        var queue = await _session.Api.GetAsync<List<OperationDto>>("api/operations/queue?take=200");
        Queue.Clear();
        foreach (var operation in queue)
        {
            Queue.Add(operation);
        }

        StatusMessage = Queue.Count == 0 ? "Очередь контроля пуста." : $"Ожидают решения: {Queue.Count}.";
    }

    private async Task ApproveAsync()
    {
        if (SelectedOperation == null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var result = await _session.Api.PostAsync<OperationDto>(
                $"api/operations/{SelectedOperation.Id}/approve",
                new ApproveRequest { Signature = SignatureText });
            SignatureText = null;
            await FetchAsync();
            StatusMessage = $"Операция {result.StatusText.ToLowerInvariant()}. {result.RejectionReason}";
        });
    }

    private async Task RejectAsync()
    {
        if (SelectedOperation == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ReasonText))
        {
            StatusMessage = "Укажите причину отклонения операции.";
            return;
        }

        await RunAsync(async () =>
        {
            var result = await _session.Api.PostAsync<OperationDto>(
                $"api/operations/{SelectedOperation.Id}/reject",
                new RejectRequest { Reason = ReasonText.Trim() });
            ReasonText = null;
            await FetchAsync();
            StatusMessage = "Операция отклонена: " + result.StatusText;
        });
    }
}
