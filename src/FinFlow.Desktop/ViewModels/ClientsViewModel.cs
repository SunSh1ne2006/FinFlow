using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FinFlow.Contracts;
using FinFlow.Desktop.Infrastructure;

namespace FinFlow.Desktop.ViewModels;

/// <summary>Ведение клиентов: поиск, анкета ПОД/ФТ, счета, представители (требования 1, 2).</summary>
public class ClientsViewModel : ViewModelBase
{
    private readonly Session _session;
    private string _searchText;
    private int _page = 1;
    private int _pageSize = 25;
    private int _total;
    private ClientListItemDto _selectedClient;
    private ClientDetailsDto _details;
    private AccountListItemDto _selectedAccount;
    private string _kycRiskLevel;
    private string _kycNotes;
    private string _repFullName;
    private string _repIdNumber;
    private string _repAuthority;
    private DateTime? _repValidFrom = DateTime.Today;
    private DateTime? _repValidTo = DateTime.Today.AddYears(1);

    public ClientsViewModel(Session session)
    {
        _session = session;
        Clients = new ObservableCollection<ClientListItemDto>();
        Accounts = new ObservableCollection<AccountListItemDto>();
        Representatives = new ObservableCollection<RepresentativeDto>();

        SearchCommand = new AsyncRelayCommand(() => LoadListAsync(1));
        NextPageCommand = new AsyncRelayCommand(() => LoadListAsync(_page + 1), () => _page * _pageSize < _total);
        PrevPageCommand = new AsyncRelayCommand(() => LoadListAsync(_page - 1), () => _page > 1);
        SaveKycCommand = new AsyncRelayCommand(SaveKycAsync, () => _details != null);
        MsiSyncCommand = new AsyncRelayCommand(MsiSyncAsync, () => _details != null);
        AddRepresentativeCommand = new AsyncRelayCommand(AddRepresentativeAsync, () => _details != null);
        OpenAccountCommand = new RelayCommand(OpenAccount, () => _selectedAccount != null);
    }

    public event Action<AccountListItemDto> AccountSelected;

    public ObservableCollection<ClientListItemDto> Clients { get; }
    public ObservableCollection<AccountListItemDto> Accounts { get; }
    public ObservableCollection<RepresentativeDto> Representatives { get; }

    public List<string> RiskLevels { get; } = new() { "Low", "Medium", "High" };

    public string SearchText
    {
        get => _searchText;
        set => Set(ref _searchText, value);
    }

    public ClientListItemDto SelectedClient
    {
        get => _selectedClient;
        set
        {
            if (Set(ref _selectedClient, value) && value != null)
            {
                _ = LoadDetailsAsync(value.Id);
            }
        }
    }

    public ClientDetailsDto Details
    {
        get => _details;
        private set
        {
            if (Set(ref _details, value))
            {
                OnPropertyChanged(nameof(ClientCaption));
                OnPropertyChanged(nameof(ClientInfo));
            }
        }
    }

    public AccountListItemDto SelectedAccount
    {
        get => _selectedAccount;
        set => Set(ref _selectedAccount, value);
    }

    public string ClientCaption => Details == null ? "Клиент не выбран" : Details.Name;

    public string ClientInfo => Details == null
        ? "Выберите клиента в списке слева."
        : $"{Details.TypeText} · {Details.IdNumber} · {Details.IdentificationStatus} · риск {Details.KycRiskLevel} · счетов: {Details.AccountsCount}";

    public string PageCaption => _total == 0 ? "Нет записей" : $"Страница {_page} · всего {_total}";

    public string KycRiskLevel
    {
        get => _kycRiskLevel;
        set => Set(ref _kycRiskLevel, value);
    }

    public string KycNotes
    {
        get => _kycNotes;
        set => Set(ref _kycNotes, value);
    }

    public string RepFullName
    {
        get => _repFullName;
        set => Set(ref _repFullName, value);
    }

    public string RepIdNumber
    {
        get => _repIdNumber;
        set => Set(ref _repIdNumber, value);
    }

    public string RepAuthority
    {
        get => _repAuthority;
        set => Set(ref _repAuthority, value);
    }

    public DateTime? RepValidFrom
    {
        get => _repValidFrom;
        set => Set(ref _repValidFrom, value);
    }

    public DateTime? RepValidTo
    {
        get => _repValidTo;
        set => Set(ref _repValidTo, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }
    public ICommand SaveKycCommand { get; }
    public ICommand MsiSyncCommand { get; }
    public ICommand AddRepresentativeCommand { get; }
    public ICommand OpenAccountCommand { get; }

    public async Task RefreshAsync()
    {
        if (Details != null)
        {
            await LoadDetailsAsync(Details.Id);
        }
        else
        {
            await LoadListAsync(_page);
        }
    }

    private async Task LoadListAsync(int page)
    {
        if (page < 1)
        {
            page = 1;
        }

        await RunAsync(async () =>
        {
            var url = $"api/clients?page={page}&pageSize={_pageSize}";
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                url += "&query=" + Uri.EscapeDataString(SearchText.Trim());
            }

            var result = await _session.Api.GetAsync<PagedResult<ClientListItemDto>>(url);
            Clients.Clear();
            foreach (var item in result.Items)
            {
                Clients.Add(item);
            }

            _page = result.Page;
            _total = result.Total;
            OnPropertyChanged(nameof(PageCaption));
        });
    }

    private async Task LoadDetailsAsync(Guid clientId)
    {
        await RunAsync(() => FetchDetailsAsync(clientId));
    }

    private async Task FetchDetailsAsync(Guid clientId)
    {
        var details = await _session.Api.GetAsync<ClientDetailsDto>($"api/clients/{clientId}");
        Details = details;

        Accounts.Clear();
        foreach (var account in details.Accounts ?? Array.Empty<AccountListItemDto>())
        {
            Accounts.Add(account);
        }

        Representatives.Clear();
        foreach (var representative in details.Representatives ?? Array.Empty<RepresentativeDto>())
        {
            Representatives.Add(representative);
        }

        KycRiskLevel = details.Kyc?.RiskLevel ?? details.KycRiskLevel;
        KycNotes = details.Kyc?.Notes;
    }

    private async Task SaveKycAsync()
    {
        if (Details == null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var updated = await _session.Api.PutAsync<ClientDetailsDto>(
                $"api/clients/{Details.Id}/kyc",
                new UpdateKycRequest { RiskLevel = KycRiskLevel, Notes = KycNotes });
            Details = updated;
            StatusMessage = "Анкета ПОД/ФТ обновлена.";
        });
    }

    private async Task MsiSyncAsync()
    {
        if (Details == null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var updated = await _session.Api.PostAsync<ClientDetailsDto>($"api/clients/{Details.Id}/msi-sync");
            Details = updated;
            StatusMessage = "Данные синхронизированы с МСИ.";
        });
    }

    private async Task AddRepresentativeAsync()
    {
        if (Details == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(RepFullName))
        {
            StatusMessage = "Укажите ФИО представителя.";
            return;
        }

        var clientId = Details.Id;
        await RunAsync(async () =>
        {
            var request = new AddRepresentativeRequest
            {
                FullName = RepFullName.Trim(),
                IdNumber = RepIdNumber?.Trim(),
                Authority = RepAuthority?.Trim(),
                ValidFrom = DateOnly.FromDateTime(RepValidFrom ?? DateTime.Today),
                ValidTo = DateOnly.FromDateTime(RepValidTo ?? DateTime.Today.AddYears(1))
            };

            await _session.Api.PostAsync<RepresentativeDto>($"api/clients/{clientId}/representatives", request);
            await FetchDetailsAsync(clientId);
            StatusMessage = "Представитель добавлен.";
        });
    }

    private void OpenAccount()
    {
        if (SelectedAccount == null)
        {
            return;
        }

        AccountSelected?.Invoke(SelectedAccount);
    }
}
