using System.Globalization;
using CoordinatesTinyApp.Infrastructure;
using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;

namespace CoordinatesTinyApp.ViewModels;

internal sealed class DatabaseSettingsViewModel : StatusViewModel
{
    private readonly IDatabaseSettingsStore _store;
    private readonly ICoordinateDatabase _database;
    private CancellationTokenSource? _operation;
    private bool _isClosed;
    private bool _isBusy;
    private string _server = "";
    private string _port = "";
    private string _databaseName = "";
    private string _username = "";
    private string _password = "";

    public DatabaseSettingsViewModel(IDatabaseSettingsStore store, ICoordinateDatabase database)
    {
        _store = store;
        _database = database;
        SaveCommand = new RelayCommand(Save, () => IsIdle && !_isClosed);
        TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, () => IsIdle && !_isClosed);
        SettingsPath = $"Файл настроек: {store.FilePath}";
        var settings = new DatabaseConnectionSettings();

        try
        {
            settings = store.Load() ?? settings;
        }
        catch (Exception exception) when (DatabaseErrorMessages.IsExpected(exception))
        {
            ShowError(DatabaseErrorMessages.GetMessage(exception) + " Заполните поля и сохраните настройки заново.");
        }

        Server = settings.Server;
        Port = settings.Port.ToString(CultureInfo.InvariantCulture);
        DatabaseName = settings.Database;
        Username = settings.Username;
        Password = settings.Password;
    }

    public event Action? CloseRequested;

    public RelayCommand SaveCommand { get; }

    public AsyncRelayCommand TestConnectionCommand { get; }

    public string SettingsPath { get; }

    public string Server
    {
        get => _server;
        set => SetProperty(ref _server, value);
    }

    public string Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string DatabaseName
    {
        get => _databaseName;
        set => SetProperty(ref _databaseName, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool IsIdle => !_isBusy;

    public void Close()
    {
        _isClosed = true;
        _operation?.Cancel();
        Password = "";
        NotifyCommands();
    }

    private DatabaseConnectionSettings ReadSettings()
    {
        if (!Int32.TryParse(Port, NumberStyles.None, CultureInfo.InvariantCulture, out int port))
            throw new FormatException("Порт должен быть целым числом от 1 до 65535.");

        var settings = new DatabaseConnectionSettings
        {
            Server = Server.Trim(),
            Port = port,
            Database = DatabaseName.Trim(),
            Username = Username.Trim(),
            Password = Password
        };
        settings.Validate();
        return settings;
    }

    private void Save()
    {
        try
        {
            _store.Save(ReadSettings());
            CloseRequested?.Invoke();
        }
        catch (FormatException exception)
        {
            ShowError(exception.Message);
        }
        catch (Exception exception) when (DatabaseErrorMessages.IsExpected(exception))
        {
            ShowError(DatabaseErrorMessages.GetMessage(exception));
        }
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            var settings = ReadSettings();
            SetBusy(true);
            ShowStatus("Проверяется подключение…");
            using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _operation = operation;
            await _database.TestConnectionAsync(settings, operation.Token);

            if (!_isClosed)
                ShowStatus("Подключение успешно. Для сохранения настроек нажмите «Сохранить».");
        }
        catch (OperationCanceledException)
        {
            if (!_isClosed)
                ShowError("Превышено время ожидания подключения.");
        }
        catch (FormatException exception)
        {
            ShowError(exception.Message);
        }
        catch (Exception exception) when (DatabaseErrorMessages.IsExpected(exception))
        {
            if (!_isClosed)
                ShowError(DatabaseErrorMessages.GetMessage(exception));
        }
        finally
        {
            _operation = null;

            if (!_isClosed)
                SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        OnPropertyChanged(nameof(IsIdle));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        SaveCommand.NotifyCanExecuteChanged();
        TestConnectionCommand.NotifyCanExecuteChanged();
    }

    private void ShowError(string message)
    {
        ShowStatus(message, "Firebrick");
    }
}
