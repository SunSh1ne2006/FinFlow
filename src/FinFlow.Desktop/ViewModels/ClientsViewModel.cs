using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FinFlow.Contracts;
using FinFlow.Desktop.Infrastructure;

namespace FinFlow.Desktop.ViewModels;

/// <summary>Работа сотрудника с клиентами: поиск, регистрация, открытие счетов и вкладов.</summary>
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

    private string _newType = "Individual";
    private string _newName;
    private string _newIdNumber;
    private DateTime? _newBirthDate;
    private string _newPhone;
    private string _newEmail;

    private ProductVersionOption _selectedProduct;
    private string _openAmountText;

    public ClientsViewModel(Session session)
    {
        _session = session;
        Clients = new ObservableCollection<ClientListItemDto>();
        Accounts = new ObservableCollection<AccountListItemDto>();
        Products = new ObservableCollection<ProductVersionOption>();

        SearchCommand = new AsyncRelayCommand(() => LoadListAsync(1));
        NextPageCommand = new AsyncRelayCommand(() => LoadListAsync(_page + 1), () => _page * _pageSize < _total);
        PrevPageCommand = new AsyncRelayCommand(() => LoadListAsync(_page - 1), () => _page > 1);
        CreateClientCommand = new AsyncRelayCommand(CreateClientAsync);
        OpenAccountCommand = new RelayCommand(OpenAccountInCard, () => _selectedAccount != null);
        OpenContractCommand = new AsyncRelayCommand(OpenContractAsync, () => _details != null && _selectedProduct != null);
    }

    public event Action<AccountListItemDto> AccountSelected;

    public ObservableCollection<ClientListItemDto> Clients { get; }
    public ObservableCollection<AccountListItemDto> Accounts { get; }
    public ObservableCollection<ProductVersionOption> Products { get; }

    public List<string> ClientTypes { get; } = new() { "Individual", "LegalEntity", "Entrepreneur" };

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
        ? "Выберите клиента в списке слева или зарегистрируйте нового."
        : $"{Details.TypeText} · {Details.IdNumber} · {Details.IdentificationStatusText} · счетов: {Details.AccountsCount}";

    public string PageCaption => _total == 0 ? "Нет записей" : $"Страница {_page} · всего {_total}";

    public string NewType
    {
        get => _newType;
        set => Set(ref _newType, value);
    }

    public string NewName
    {
        get => _newName;
        set => Set(ref _newName, value);
    }

    public string NewIdNumber
    {
        get => _newIdNumber;
        set => Set(ref _newIdNumber, value);
    }

    public DateTime? NewBirthDate
    {
        get => _newBirthDate;
        set => Set(ref _newBirthDate, value);
    }

    public string NewPhone
    {
        get => _newPhone;
        set => Set(ref _newPhone, value);
    }

    public string NewEmail
    {
        get => _newEmail;
        set => Set(ref _newEmail, value);
    }

    public ProductVersionOption SelectedProduct
    {
        get => _selectedProduct;
        set => Set(ref _selectedProduct, value);
    }

    public string OpenAmountText
    {
        get => _openAmountText;
        set => Set(ref _openAmountText, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }
    public ICommand CreateClientCommand { get; }
    public ICommand OpenAccountCommand { get; }
    public ICommand OpenContractCommand { get; }

    public async Task RefreshAsync()
    {
        if (Products.Count == 0)
        {
            await LoadProductsAsync();
        }

        if (Details != null)
        {
            await LoadDetailsAsync(Details.Id);
        }
        else
        {
            await LoadListAsync(_page);
        }
    }

    private async Task LoadProductsAsync()
    {
        await RunAsync(async () =>
        {
            var products = await _session.Api.GetAsync<List<ProductDto>>("api/products?kind=Deposit");
            var current = await _session.Api.GetAsync<List<ProductDto>>("api/products?kind=CurrentAccount");

            Products.Clear();
            foreach (var product in current.Concat(products))
            {
                var version = product.Versions?.FirstOrDefault(v => v.IsApproved);
                if (version != null)
                {
                    Products.Add(ProductVersionOption.Create(product, version));
                }
            }

            SelectedProduct = Products.FirstOrDefault();
        });
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
    }

    private async Task CreateClientAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewIdNumber))
        {
            StatusMessage = "Укажите ФИО (наименование) и идентификационный номер.";
            return;
        }

        await RunAsync(async () =>
        {
            var created = await _session.Api.PostAsync<ClientDetailsDto>("api/clients", new CreateClientRequest
            {
                Type = NewType,
                Name = NewName.Trim(),
                IdNumber = NewIdNumber.Trim(),
                BirthDate = NewBirthDate.HasValue ? DateOnly.FromDateTime(NewBirthDate.Value) : null,
                Phone = NewPhone?.Trim(),
                Email = NewEmail?.Trim()
            });

            NewName = null;
            NewIdNumber = null;
            NewBirthDate = null;
            NewPhone = null;
            NewEmail = null;

            await LoadListAsync(1);
            Details = created;
            SelectedClient = Clients.FirstOrDefault(c => c.Id == created.Id);
            StatusMessage = $"Клиент {created.Name} зарегистрирован и идентифицирован.";
        });
    }

    private async Task OpenContractAsync()
    {
        if (Details == null || SelectedProduct == null)
        {
            return;
        }

        if (!AccountViewModel.TryParseAmount(OpenAmountText, out var amount) || amount < 0)
        {
            StatusMessage = "Укажите неотрицательную сумму.";
            return;
        }

        if (amount < SelectedProduct.MinAmount)
        {
            StatusMessage = $"Минимальная сумма по продукту – {SelectedProduct.MinAmount:N2} {SelectedProduct.Currency}.";
            return;
        }

        var clientId = Details.Id;
        await RunAsync(async () =>
        {
            if (SelectedProduct.IsCurrentAccount)
            {
                await _session.Api.PostAsync<AccountDetailsDto>("api/accounts", new OpenAccountRequest
                {
                    ClientId = clientId,
                    ProductId = SelectedProduct.ProductId,
                    Currency = SelectedProduct.Currency,
                    InitialAmount = amount
                });
            }
            else
            {
                await _session.Api.PostAsync<ContractDto>("api/contracts", new OpenContractRequest
                {
                    ClientId = clientId,
                    ProductVersionId = SelectedProduct.VersionId,
                    Amount = amount
                });
            }

            await FetchDetailsAsync(clientId);
            await LoadListAsync(_page);
            OpenAmountText = null;
            StatusMessage = "Договор открыт, счёт и операция первоначального взноса созданы.";
        });
    }

    private void OpenAccountInCard()
    {
        if (SelectedAccount == null)
        {
            return;
        }

        AccountSelected?.Invoke(SelectedAccount);
    }
}
