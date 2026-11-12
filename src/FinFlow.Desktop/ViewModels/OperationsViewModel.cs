using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using FinFlow.Contracts;
using FinFlow.Desktop.Infrastructure;

namespace FinFlow.Desktop.ViewModels;

/// <summary>Ввод и сопровождение операций по счёту (требования 5, 6, 7, 9, 12).</summary>
public class OperationsViewModel : ViewModelBase
{
    private readonly Session _session;
    private string _accountNumber;
    private AccountDetailsDto _account;
    private OperationKindOption _selectedKind;
    private string _amountText;
    private string _description;
    private string _documentNumber;
    private OperationDto _selectedOperation;
    private string _reasonText;

    public OperationsViewModel(Session session)
    {
        _session = session;
        KindOptions = new List<OperationKindOption>
        {
            new("CashIn", "Взнос наличными"),
            new("TransferIn", "Безналичное поступление"),
            new("CashOut", "Выдача наличными"),
            new("TransferOut", "Безналичное списание"),
            new("PartialWithdrawal", "Частичное изъятие вклада"),
            new("EarlyReturn", "Досрочный возврат вклада"),
            new("InterestPayment", "Выплата процентов"),
            new("Capitalization", "Капитализация процентов")
        };
        SelectedKind = KindOptions[0];
        Operations = new ObservableCollection<OperationDto>();

        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !string.IsNullOrWhiteSpace(AccountNumber));
        CreateCommand = new AsyncRelayCommand(CreateAsync, () => !string.IsNullOrWhiteSpace(AccountNumber));
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => _selectedOperation != null);
        RefreshOperationCommand = new AsyncRelayCommand(LoadAsync, () => !string.IsNullOrWhiteSpace(AccountNumber));
        ReverseCommand = new AsyncRelayCommand(ReverseAsync, () => _selectedOperation != null);
    }

    public List<OperationKindOption> KindOptions { get; }

    public ObservableCollection<OperationDto> Operations { get; }

    public string AccountNumber
    {
        get => _accountNumber;
        set => Set(ref _accountNumber, value);
    }

    public AccountDetailsDto Account
    {
        get => _account;
        private set
        {
            if (Set(ref _account, value))
            {
                OnPropertyChanged(nameof(AccountCaption));
            }
        }
    }

    public string AccountCaption => Account == null
        ? "Счёт не загружен."
        : $"{Account.Number} · {Account.ClientName} · доступно {Account.AvailableBalanceCalculated:N2} {Account.Currency} · остаток {Account.Balance:N2}";

    public OperationKindOption SelectedKind
    {
        get => _selectedKind;
        set => Set(ref _selectedKind, value);
    }

    public string AmountText
    {
        get => _amountText;
        set => Set(ref _amountText, value);
    }

    public string Description
    {
        get => _description;
        set => Set(ref _description, value);
    }

    public string DocumentNumber
    {
        get => _documentNumber;
        set => Set(ref _documentNumber, value);
    }

    public OperationDto SelectedOperation
    {
        get => _selectedOperation;
        set
        {
            if (Set(ref _selectedOperation, value))
            {
                OnPropertyChanged(nameof(SelectedOperationCaption));
            }
        }
    }

    public string SelectedOperationCaption => SelectedOperation == null
        ? "Операция не выбрана."
        : $"{SelectedOperation.KindText} · {SelectedOperation.Amount:N2} {SelectedOperation.Currency} · {SelectedOperation.StatusText}"
          + (string.IsNullOrWhiteSpace(SelectedOperation.RejectionReason) ? string.Empty : " · причина: " + SelectedOperation.RejectionReason);

    public string ReasonText
    {
        get => _reasonText;
        set => Set(ref _reasonText, value);
    }

    public ICommand LoadCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand SubmitCommand { get; }
    public ICommand RefreshOperationCommand { get; }
    public ICommand ReverseCommand { get; }

    public void SetAccount(string number)
    {
        AccountNumber = number;
        Account = null;
        Operations.Clear();
        SelectedOperation = null;
    }

    public async Task RefreshAsync()
    {
        if (!string.IsNullOrWhiteSpace(AccountNumber))
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var details = await _session.Api.GetAsync<AccountDetailsDto>(
                "api/accounts/lookup?number=" + Uri.EscapeDataString(AccountNumber.Trim()));
            Account = details;

            var operations = await _session.Api.GetAsync<List<OperationDto>>("api/operations?accountId=" + details.Id + "&take=100");
            Operations.Clear();
            foreach (var operation in operations)
            {
                Operations.Add(operation);
            }
        });
    }

    private async Task CreateAsync()
    {
        if (Account == null)
        {
            await LoadAsync();
        }

        if (Account == null)
        {
            StatusMessage = "Сначала загрузите счёт.";
            return;
        }

        if (!AccountViewModel.TryParseAmount(AmountText, out var amount) || amount <= 0)
        {
            StatusMessage = "Укажите положительную сумму операции.";
            return;
        }

        await RunAsync(async () =>
        {
            var request = new CreateOperationRequest
            {
                AccountId = Account.Id,
                ContractId = Account.Contract?.Id,
                Kind = SelectedKind.Code,
                Amount = amount,
                Description = string.IsNullOrWhiteSpace(Description) ? SelectedKind.Name : Description.Trim(),
                DocumentNumber = string.IsNullOrWhiteSpace(DocumentNumber) ? null : DocumentNumber.Trim(),
                SourceSystem = "Branch"
            };

            var created = await _session.Api.PostAsync<OperationDto>("api/operations", request);
            Operations.Insert(0, created);
            SelectedOperation = created;
            StatusMessage = $"Операция создана (черновик). {WorkflowHint(created)}";
            AmountText = null;
            Description = null;
        });
    }

    private async Task SubmitAsync()
    {
        if (SelectedOperation == null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var result = await _session.Api.PostAsync<OperationDto>($"api/operations/{SelectedOperation.Id}/submit");
            SelectedOperation = result;
            await ReloadListAsync();
            StatusMessage = $"Операция отправлена: {result.StatusText}. {WorkflowHint(result)}";
        });
    }

    private async Task ReverseAsync()
    {
        if (SelectedOperation == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ReasonText))
        {
            StatusMessage = "Для сторнирования укажите причину (бизнес-правило 13).";
            return;
        }

        await RunAsync(async () =>
        {
            var result = await _session.Api.PostAsync<OperationDto>(
                $"api/operations/{SelectedOperation.Id}/reverse",
                new ReverseRequest { Reason = ReasonText.Trim() });
            SelectedOperation = result;
            await ReloadListAsync();
            ReasonText = null;
            StatusMessage = "Операция сторнирована; исходная запись сохранена без изменений.";
        });
    }

    private async Task ReloadListAsync()
    {
        if (Account == null)
        {
            return;
        }

        var operations = await _session.Api.GetAsync<List<OperationDto>>("api/operations?accountId=" + Account.Id + "&take=100");
        Operations.Clear();
        foreach (var operation in operations)
        {
            Operations.Add(operation);
        }
    }

    private string WorkflowHint(OperationDto operation)
    {
        return operation.Status switch
        {
            "OnCheck" => "Операция ожидает подтверждения контролёра.",
            "OnControl" => "Операция на контроле: требуется подтверждение контролёра.",
            "Suspended" => "Операция приостановлена до выяснения признаков ПОД/ФТ.",
            "Executed" => "Операция исполнена, остаток и проводки обновлены.",
            "Approved" => "Операция одобрена.",
            "Rejected" => "Операция отклонена: " + (operation.RejectionReason ?? "причина не указана"),
            _ => string.Empty
        };
    }
}
