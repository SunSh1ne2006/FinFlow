using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using FinFlow.Contracts;
using FinFlow.Desktop.Infrastructure;

namespace FinFlow.Desktop.ViewModels;

/// <summary>Карточка счёта: остаток, лимит овердрафта, аресты, доступный остаток (требования 1, 3, 4).</summary>
public class AccountViewModel : ViewModelBase
{
    private readonly Session _session;
    private string _numberSearch;
    private AccountDetailsDto _account;
    private HoldDto _selectedHold;
    private string _holdAmountText;
    private string _holdBasisNumber;
    private string _holdBasisAuthority = "АИС ИДО";
    private bool _holdIsArrest = true;
    private string _holdComment;
    private string _overdraftLimitText;

    public AccountViewModel(Session session)
    {
        _session = session;
        Holds = new ObservableCollection<HoldDto>();

        FindCommand = new AsyncRelayCommand(() => FindAsync(NumberSearch), () => !string.IsNullOrWhiteSpace(NumberSearch));
        ReleaseHoldCommand = new AsyncRelayCommand(ReleaseHoldAsync, () => _selectedHold != null && Account != null);
        AddHoldCommand = new AsyncRelayCommand(AddHoldAsync, () => Account != null);
        SetOverdraftCommand = new AsyncRelayCommand(SetOverdraftAsync, () => Account != null);
        CloseAccountCommand = new AsyncRelayCommand(CloseAccountAsync, () => Account != null);
    }

    public event Action<AccountDetailsDto> AccountOpened;

    public ObservableCollection<HoldDto> Holds { get; }

    public string NumberSearch
    {
        get => _numberSearch;
        set => Set(ref _numberSearch, value);
    }

    public AccountDetailsDto Account
    {
        get => _account;
        private set
        {
            if (Set(ref _account, value))
            {
                OnPropertyChanged(nameof(Caption));
                OnPropertyChanged(nameof(BalanceInfo));
                OnPropertyChanged(nameof(AvailabilityInfo));
                OnPropertyChanged(nameof(ContractInfo));
                OnPropertyChanged(nameof(NumbersInfo));
                OnPropertyChanged(nameof(ArrestCaption));
                OverdraftLimitText = value?.OverdraftLimit.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }
    }

    public HoldDto SelectedHold
    {
        get => _selectedHold;
        set => Set(ref _selectedHold, value);
    }

    public string Caption => Account == null
        ? "Счёт не выбран"
        : $"{Account.Number} · {Account.ClientName}";

    public string BalanceInfo => Account == null
        ? string.Empty
        : $"Остаток: {Account.Balance:N2} {Account.Currency} · лимит овердрафта: {Account.OverdraftLimit:N2} · использовано: {Account.OverdraftUsed:N2} ({Account.OverdraftUtilizationPercent:N2}%)";

    public string AvailabilityInfo => Account == null
        ? string.Empty
        : $"Аресты и резервы: {Account.HoldsAmount:N2} · доступный остаток (1.1): {Account.AvailableBalanceCalculated:N2}";

    public string ContractInfo => Account?.Contract == null
        ? "Договор не связан со счётом (текущий счёт)."
        : $"Договор {Account.Contract.Number} · {Account.Contract.ProductName} · {Account.Contract.RatePercent:N2}% · срок до {Account.Contract.EndDate:dd.MM.yyyy} · {Account.Contract.StatusText}";

    public string NumbersInfo
    {
        get
        {
            if (Account?.Numbers == null || Account.Numbers.Count == 0)
            {
                return "Номера счёта не заданы.";
            }

            var parts = new System.Collections.Generic.List<string>();
            foreach (var number in Account.Numbers)
            {
                parts.Add($"{number.Kind}: {number.Number}");
            }

            return string.Join(" · ", parts);
        }
    }

    public string ArrestCaption => Account == null
        ? string.Empty
        : Account.HasArrest ? "По счёту действует арест: расходные операции блокируются (L = 0)." : "Арестов нет.";

    public string HoldAmountText
    {
        get => _holdAmountText;
        set => Set(ref _holdAmountText, value);
    }

    public string HoldBasisNumber
    {
        get => _holdBasisNumber;
        set => Set(ref _holdBasisNumber, value);
    }

    public string HoldBasisAuthority
    {
        get => _holdBasisAuthority;
        set => Set(ref _holdBasisAuthority, value);
    }

    public bool HoldIsArrest
    {
        get => _holdIsArrest;
        set => Set(ref _holdIsArrest, value);
    }

    public string HoldComment
    {
        get => _holdComment;
        set => Set(ref _holdComment, value);
    }

    public string OverdraftLimitText
    {
        get => _overdraftLimitText;
        set => Set(ref _overdraftLimitText, value);
    }

    public ICommand FindCommand { get; }
    public ICommand ReleaseHoldCommand { get; }
    public ICommand AddHoldCommand { get; }
    public ICommand SetOverdraftCommand { get; }
    public ICommand CloseAccountCommand { get; }

    public async Task OpenByNumberAsync(string number)
    {
        NumberSearch = number;
        await FindAsync(number);
    }

    public async Task RefreshAsync()
    {
        if (!string.IsNullOrWhiteSpace(NumberSearch))
        {
            await FindAsync(NumberSearch);
        }
    }

    private async Task FindAsync(string number)
    {
        if (string.IsNullOrWhiteSpace(number))
        {
            StatusMessage = "Укажите номер счёта.";
            return;
        }

        await RunAsync(() => FetchAsync(number));
    }

    private async Task FetchAsync(string number)
    {
        var details = await _session.Api.GetAsync<AccountDetailsDto>("api/accounts/lookup?number=" + Uri.EscapeDataString(number.Trim()));
        Apply(details);
    }

    private async Task ReleaseHoldAsync()
    {
        if (SelectedHold == null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var number = Account.Number;
            await _session.Api.PostAsync($"api/accounts/holds/{SelectedHold.Id}/release");
            await FetchAsync(number);
            StatusMessage = "Обеспечение снято, доступный остаток пересчитан.";
        });
    }

    private async Task AddHoldAsync()
    {
        if (Account == null)
        {
            return;
        }

        if (!TryParseAmount(HoldAmountText, out var amount) || amount <= 0)
        {
            StatusMessage = "Сумма ареста должна быть положительной.";
            return;
        }

        await RunAsync(async () =>
        {
            var request = new CreateHoldRequest
            {
                Source = "AisIdo",
                BasisNumber = HoldBasisNumber?.Trim(),
                BasisAuthority = HoldBasisAuthority?.Trim(),
                Amount = amount,
                IsArrest = HoldIsArrest,
                Comment = HoldComment?.Trim()
            };

            var number = Account.Number;
            await _session.Api.PostAsync<HoldDto>($"api/accounts/{Account.Id}/holds", request);
            await FetchAsync(number);
            StatusMessage = "Обеспечение зарегистрировано.";
        });
    }

    private async Task SetOverdraftAsync()
    {
        if (Account == null)
        {
            return;
        }

        if (!TryParseAmount(OverdraftLimitText, out var limit) || limit < 0)
        {
            StatusMessage = "Лимит овердрафта должен быть неотрицательным.";
            return;
        }

        await RunAsync(async () =>
        {
            var updated = await _session.Api.PutAsync<AccountDetailsDto>(
                $"api/accounts/{Account.Id}/overdraft",
                new SetOverdraftRequest { OverdraftLimit = limit });
            Apply(updated);
            StatusMessage = "Лимит овердрафта изменён.";
        });
    }

    private async Task CloseAccountAsync()
    {
        if (Account == null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var updated = await _session.Api.PostAsync<AccountDetailsDto>($"api/accounts/{Account.Id}/close");
            Apply(updated);
            StatusMessage = "Счёт закрыт.";
        });
    }

    private void Apply(AccountDetailsDto details)
    {
        Account = details;
        Holds.Clear();
        foreach (var hold in details?.Holds ?? Array.Empty<HoldDto>())
        {
            Holds.Add(hold);
        }

        if (details != null)
        {
            AccountOpened?.Invoke(details);
        }
    }

    internal static bool TryParseAmount(string text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim().Replace(" ", string.Empty).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }
}
