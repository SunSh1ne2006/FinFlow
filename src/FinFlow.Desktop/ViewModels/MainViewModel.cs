using System.Threading.Tasks;
using FinFlow.Contracts;
using FinFlow.Desktop.Infrastructure;

namespace FinFlow.Desktop.ViewModels;

/// <summary>Корневая модель представления операционного контура.</summary>
public class MainViewModel : ObservableObject
{
    private int _selectedTabIndex;

    public MainViewModel(Session session)
    {
        Session = session;
        Clients = new ClientsViewModel(session);
        Account = new AccountViewModel(session);
        Operations = new OperationsViewModel(session);
        ControlQueue = new ControlQueueViewModel(session);
        DayClose = new DayCloseViewModel(session);
        Admin = new AdminViewModel(session);

        Clients.AccountSelected += OnAccountSelected;
        Account.AccountOpened += OnAccountDetailsOpened;
    }

    public Session Session { get; }
    public ClientsViewModel Clients { get; }
    public AccountViewModel Account { get; }
    public OperationsViewModel Operations { get; }
    public ControlQueueViewModel ControlQueue { get; }
    public DayCloseViewModel DayClose { get; }
    public AdminViewModel Admin { get; }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => Set(ref _selectedTabIndex, value);
    }

    public string UserCaption => Session.Caption;

    public string StatusMessage => "Сервер приложений: " + ApiSettings.BaseUrl + " · сеанс " + Session.User.Login;

    public bool CanControl => Session.IsInRole("Controller") || Session.IsInRole("Administrator");

    public bool CanAdminister => Session.IsInRole("Administrator");

    public bool CanCloseDay => Session.IsInRole("Accountant") || Session.IsInRole("Administrator") || Session.IsInRole("Head");

    public async Task InitializeAsync()
    {
        await Clients.RefreshAsync();

        if (CanCloseDay)
        {
            await DayClose.RefreshAsync();
        }
    }

    public async Task RefreshTabAsync(int index)
    {
        switch (index)
        {
            case 0:
                await Clients.RefreshAsync();
                break;
            case 1:
                await Account.RefreshAsync();
                break;
            case 2:
                await Operations.RefreshAsync();
                break;
            case 3:
                if (CanControl)
                {
                    await ControlQueue.RefreshAsync();
                }
                break;
            case 4:
                if (CanCloseDay)
                {
                    await DayClose.RefreshAsync();
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
