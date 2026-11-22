using System.Threading.Tasks;
using FinFlow.Contracts;
using FinFlow.Desktop.Infrastructure;

namespace FinFlow.Desktop.ViewModels;

/// <summary>Корневая модель представления рабочего места сотрудника банка.</summary>
public class MainViewModel : ObservableObject
{
    private int _selectedTabIndex;

    public MainViewModel(Session session)
    {
        Session = session;
        Clients = new ClientsViewModel(session);
        Account = new AccountViewModel(session);
        Operations = new OperationsViewModel(session);
        DayClose = new DayCloseViewModel(session);
        Reports = new ReportsViewModel(session);
        Admin = new AdminViewModel(session);

        Clients.AccountSelected += OnAccountSelected;
        Account.AccountOpened += OnAccountDetailsOpened;

        if (!CanServeClients)
        {
            _selectedTabIndex = CanAdminister ? 5 : 4;
        }
    }

    public Session Session { get; }
    public ClientsViewModel Clients { get; }
    public AccountViewModel Account { get; }
    public OperationsViewModel Operations { get; }
    public DayCloseViewModel DayClose { get; }
    public ReportsViewModel Reports { get; }
    public AdminViewModel Admin { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => Set(ref _selectedTabIndex, value);
    }

    public string UserCaption => Session.Caption;

    public string StatusMessage => "Сервер приложений: " + ApiSettings.BaseUrl + " · сеанс " + Session.User.Login;

    public bool CanServeClients => Session.IsInRole("Employee");

    public bool CanCloseDay => Session.IsInRole("Employee") || Session.IsInRole("Administrator");

    public bool CanReport => Session.IsInRole("Head") || Session.IsInRole("Administrator");

    public bool CanAdminister => Session.IsInRole("Administrator");

    public async Task InitializeAsync()
    {
        if (CanServeClients)
        {
            await Clients.RefreshAsync();
        }

        if (CanReport)
        {
            await Reports.RefreshAsync();
        }
    }

    public async Task RefreshTabAsync(int index)
    {
        switch (index)
        {
            case 0:
                if (CanServeClients)
                {
                    await Clients.RefreshAsync();
                }
                break;
            case 1:
                if (CanServeClients)
                {
                    await Account.RefreshAsync();
                }
                break;
            case 2:
                if (CanServeClients)
                {
                    await Operations.RefreshAsync();
                }
                break;
            case 3:
                if (CanCloseDay)
                {
                    await DayClose.RefreshAsync();
                }
                break;
            case 4:
                if (CanReport)
                {
                    await Reports.RefreshAsync();
                }
                break;
            case 5:
                if (CanAdminister)
                {
                    await Admin.RefreshAsync();
                }
                break;
        }
    }

    private void OnAccountSelected(AccountListItemDto account)
    {
        if (account == null)
        {
            return;
        }

        Operations.SetAccount(account.Number);
        SelectedTabIndex = 1;
        _ = Account.OpenByNumberAsync(account.Number);
    }

    private void OnAccountDetailsOpened(AccountDetailsDto account)
    {
        if (account != null)
        {
            Operations.SetAccount(account.Number);
        }
    }
}
