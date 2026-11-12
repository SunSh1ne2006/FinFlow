using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using FinFlow.Contracts;
using FinFlow.Desktop.Infrastructure;

namespace FinFlow.Desktop.ViewModels;

/// <summary>Административные функции: пользователи, параметры, журнал аудита (требования 16, 23).</summary>
public class AdminViewModel : ViewModelBase
{
    private readonly Session _session;
    private ParameterDto _selectedParameter;
    private string _parameterValueText;
    private string _parameterName;
    private UserDto _selectedUser;
    private string _newLogin;
    private string _newFullName;
    private string _newPassword;
    private string _newRole = "Operator";
    private bool _newTwoFactor;

    public AdminViewModel(Session session)
    {
        _session = session;
        Parameters = new ObservableCollection<ParameterDto>();
        Audit = new ObservableCollection<AuditRecordDto>();
        Users = new ObservableCollection<UserDto>();
        Roles = new List<string> { "Operator", "Controller", "Accountant", "Administrator", "Head", "Treasurer", "RiskManager", "AmlOfficer" };

        RefreshCommand = new AsyncRelayCommand(LoadAsync);
        SetParameterCommand = new AsyncRelayCommand(SetParameterAsync, () => _selectedParameter != null);
        CreateUserCommand = new AsyncRelayCommand(CreateUserAsync);
        BlockUserCommand = new AsyncRelayCommand(BlockUserAsync, () => _selectedUser != null);
    }

    public ObservableCollection<ParameterDto> Parameters { get; }
    public ObservableCollection<AuditRecordDto> Audit { get; }
    public ObservableCollection<UserDto> Users { get; }
    public List<string> Roles { get; }

    public ParameterDto SelectedParameter
    {
        get => _selectedParameter;
        set
        {
            if (Set(ref _selectedParameter, value))
            {
                ParameterValueText = value?.Value.ToString("0.####");
                ParameterName = value?.Name;
            }
        }
    }

    public string ParameterValueText
    {
        get => _parameterValueText;
        set => Set(ref _parameterValueText, value);
    }

    public string ParameterName
    {
        get => _parameterName;
        set => Set(ref _parameterName, value);
    }

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

    public bool NewTwoFactor
    {
        get => _newTwoFactor;
        set => Set(ref _newTwoFactor, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand SetParameterCommand { get; }
    public ICommand CreateUserCommand { get; }
    public ICommand BlockUserCommand { get; }

    public async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await RunAsync(FetchAsync);
    }

    private async Task FetchAsync()
    {
        var parameters = await _session.Api.GetAsync<List<ParameterDto>>("api/parameters");
        Parameters.Clear();
        foreach (var parameter in parameters)
        {
            Parameters.Add(parameter);
        }

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

    private async Task SetParameterAsync()
    {
        if (SelectedParameter == null)
        {
            return;
        }

        if (!AccountViewModel.TryParseAmount(ParameterValueText, out var value))
        {
            StatusMessage = "Значение параметра должно быть числом.";
            return;
        }

        await RunAsync(async () =>
        {
            await _session.Api.PostAsync<ParameterDto>("api/parameters", new SetParameterRequest
            {
                Code = SelectedParameter.Code,
                Name = string.IsNullOrWhiteSpace(ParameterName) ? SelectedParameter.Name : ParameterName,
                Unit = SelectedParameter.Unit,
                Value = value
            });
            await FetchAsync();
            StatusMessage = "Параметр сохранён с новой датой действия; предыдущее значение осталось в истории.";
        });
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
                Role = NewRole,
                TwoFactorEnabled = NewTwoFactor
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
