using System;
using System.IO;
using System.Windows;

namespace FinFlow.Desktop;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnUnhandledException;
    }

    /// <summary>
    /// Необработанные исключения интерфейса показываются пользователю и пишутся в журнал,
    /// чтобы ошибка запуска не выглядела как «программа просто закрылась».
    /// </summary>
    private static void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FinFlow");
        var logPath = Path.Combine(folder, "error.log");

        try
        {
            Directory.CreateDirectory(folder);
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
            logPath = "журнал недоступен";
        }

        MessageBox.Show(
            "Непредвиденная ошибка: " + e.Exception.Message + Environment.NewLine + Environment.NewLine +
            "Подробности записаны в " + logPath + "." + Environment.NewLine +
            "Проверьте, что сервер приложений FinFlow.Api запущен, и повторите действие.",
            "FinFlow", MessageBoxButton.OK, MessageBoxImage.Error);

        e.Handled = true;
    }
}
