using System;
using System.Threading.Tasks;

namespace FinFlow.Desktop.Infrastructure;

/// <summary>Базовое представление: обработка ошибок сервера и состояние занятости.</summary>
public abstract class ViewModelBase : ObservableObject
{
    private string _statusMessage;
    private bool _isBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        protected set => Set(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        protected set => Set(ref _isBusy, value);
    }

    protected async Task RunAsync(Func<Task> action, string successMessage = null)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        try
        {
            await action();
            if (successMessage != null)
            {
                StatusMessage = successMessage;
            }
        }
        catch (ApiException exception)
        {
            StatusMessage = exception.Message;
        }
        catch (Exception exception)
        {
            StatusMessage = "Ошибка: " + exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
