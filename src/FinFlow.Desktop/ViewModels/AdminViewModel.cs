using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using FinFlow.Contracts;
using FinFlow.Desktop.Infrastructure;

namespace FinFlow.Desktop.ViewModels;

/// <summary>Администрирование: учётные записи сотрудников и журнал аудита.</summary>
public class AdminViewModel : ViewModelBase
{
    private readonly Session _session;
    private UserDto _selectedUser;
    private string _newLogin;
    private string _newFullName;
    private string _newPassword;
    private string _newRole = "Employee";

    public AdminViewModel(Session session)
    {
        _session = session;
        Audit = new ObservableCollection<AuditRecordDto>();
        Users = new ObservableCollection<UserDto>();
        Roles = new List<string> { "Employee", "Administrator", "Head" };

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        CreateUserCommand = new AsyncRelayCommand(CreateUserAsync);
        BlockUserCommand = new AsyncRelayCommand(BlockUserAsync, () => _selectedUser != null);
    }

    public ObservableCollection<AuditRecordDto> Audit { get; }
    public ObservableCollection<UserDto> Users { get; }
    public List<string> Roles { get; }

    public UserDto SelectedUser
    {
        get => _selectedUser;
        set => Set(ref _selectedUser, value);
    }

    public string NewLogin
    {
        get => _newLogin;
        set => Set(ref _newLogin, value);
    }

    public string NewFullName
    {
        get => _newFullName;
        set => Set(ref _newFullName, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => Set(ref _newPassword, value);
    }

    public string NewRole
    {
        get => _newRole;
        set => Set(ref _newRole, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand CreateUserCommand { get; }
    public ICommand BlockUserCommand { get; }

    public async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await RunAsync(FetchAsync);
    }

    private async Task FetchAsync()
    {
        var audit = await _session.Api.GetAsync<PagedResult<AuditRecordDto>>("api/audit?page=1&pageSize=100");
        Audit.Clear();
        foreach (var record in audit.Items)
        {
            Audit.Add(record);
        }

        var users = await _session.Api.GetAsync<List<UserDto>>("api/users");
        Users.Clear();
        foreach (var user in users)
        {
            Users.Add(user);
        }
    }

    private async Task CreateUserAsync()
    {
        if (string.IsNullOrWhiteSpace(NewLogin) || string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(NewFullName))
        {
            StatusMessage = "Заполните логин, ФИО и пароль.";
            return;
        }

        await RunAsync(async () =>
        {
            await _session.Api.PostAsync<UserDto>("api/users", new CreateUserRequest
            {
                Login = NewLogin.Trim(),
                FullName = NewFullName.Trim(),
                Password = NewPassword,
                Role = NewRole
            });

            NewLogin = null;
            NewFullName = null;
            NewPassword = null;
            await FetchAsync();
            StatusMessage = "Пользователь создан.";
        });
    }

    private async Task BlockUserAsync()
    {
        if (SelectedUser == null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var target = !SelectedUser.IsBlocked;
            var updated = await _session.Api.PostAsync<UserDto>(
                $"api/users/{SelectedUser.Id}/block?blocked={target.ToString().ToLowerInvariant()}");
            var index = Users.IndexOf(SelectedUser);
            if (index >= 0)
            {
                Users[index] = updated;
            }

            SelectedUser = updated;
            StatusMessage = target ? "Пользователь заблокирован." : "Пользователь разблокирован.";
        });
    }
}
