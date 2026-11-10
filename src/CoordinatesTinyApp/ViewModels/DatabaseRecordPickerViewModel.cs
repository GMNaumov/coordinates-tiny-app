using System.Globalization;
using CoordinatesTinyApp.Infrastructure;
using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;

namespace CoordinatesTinyApp.ViewModels;

internal sealed class DatabaseRecordPickerViewModel : StatusViewModel
{
    private readonly IDatabaseSettingsStore _store;
    private readonly ICoordinateDatabase _database;
    private readonly IUserDialogService _dialogs;
    private DatabaseConnectionSettings? _settings;
    private CancellationTokenSource? _operation;
    private int _offset;
    private int? _objectId;
    private Guid? _objectGuid;
    private bool _hasNext;
    private bool _isClosed;
    private bool _isBusy;
    private string _searchText = "";
    private string _connectionText = "";
    private string _pageText = "";
    private IReadOnlyList<CoordinateRecord> _records = Array.Empty<CoordinateRecord>();
    private CoordinateRecord? _selectedRecord;

    public DatabaseRecordPickerViewModel(IDatabaseSettingsStore store, ICoordinateDatabase database, IUserDialogService dialogs)
    {
        _store = store;
        _database = database;
        _dialogs = dialogs;
        SettingsCommand = new AsyncRelayCommand(ChangeSettingsAsync, CanInteract);
        SearchCommand = new AsyncRelayCommand(SearchAsync, CanInteract);
        PreviousCommand = new AsyncRelayCommand(() => LoadPageAsync(Math.Max(0, _offset - PostgresCoordinateReader.PageSize)),
            () => CanInteract() && _offset > 0);
        NextCommand = new AsyncRelayCommand(() => LoadPageAsync(_offset + PostgresCoordinateReader.PageSize),
            () => CanInteract() && _hasNext && _offset <= Int32.MaxValue - PostgresCoordinateReader.PageSize);
        SelectCommand = new RelayCommand(SelectRecord, () => CanInteract() && SelectedRecord is not null);
    }

    public event Action? CloseRequested;

    public AsyncRelayCommand SettingsCommand { get; }

    public AsyncRelayCommand SearchCommand { get; }

    public AsyncRelayCommand PreviousCommand { get; }

    public AsyncRelayCommand NextCommand { get; }

    public RelayCommand SelectCommand { get; }

    public string? SelectedPlacement { get; private set; }

    public bool IsIdle => !_isBusy;

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string ConnectionText
    {
        get => _connectionText;
        private set => SetProperty(ref _connectionText, value);
    }

    public string PageText
    {
        get => _pageText;
        private set => SetProperty(ref _pageText, value);
    }

    public IReadOnlyList<CoordinateRecord> Records
    {
        get => _records;
        private set => SetProperty(ref _records, value);
    }

    public CoordinateRecord? SelectedRecord
    {
        get => _selectedRecord;
        set
        {
            if (SetProperty(ref _selectedRecord, value))
                SelectCommand.NotifyCanExecuteChanged();
        }
    }

    public async Task InitializeAsync()
    {
        if (_isClosed)
            return;

        try
        {
            _settings = _store.Load();

            if (_settings is null)
            {
                if (!_dialogs.ShowDatabaseSettings())
                {
                    ShowStatus("Для загрузки записей укажите настройки подключения.");
                    return;
                }

                _settings = _store.Load();
            }

            ConnectionText = $"{_settings!.Server}:{_settings.Port} / {_settings.Database} — dbo.Coordinates";
            await LoadPageAsync(0);
        }
        catch (Exception exception) when (DatabaseErrorMessages.IsExpected(exception))
        {
            _settings = null;
            ShowError(DatabaseErrorMessages.GetMessage(exception) + " Откройте настройки подключения.");
        }
    }

    public void Close()
    {
        _isClosed = true;
        _operation?.Cancel();
        _settings = null;
        NotifyCommands();
    }

    private async Task ChangeSettingsAsync()
    {
        if (!_dialogs.ShowDatabaseSettings())
            return;

        ClearRecords();
        _offset = 0;
        _hasNext = false;
        _objectId = null;
        _objectGuid = null;
        SearchText = "";
        SetBusy(false);
        await InitializeAsync();
    }

    private async Task SearchAsync()
    {
        string search = SearchText.Trim();
        int? objectId = null;
        Guid? objectGuid = null;

        if (search.Length > 0)
        {
            if (Int32.TryParse(search, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int id))
            {
                objectId = id;
            }
            else if (Guid.TryParse(search, out var guid))
            {
                objectGuid = guid;
            }
            else
            {
                ShowError("Введите целочисленный ID объекта или GUID. Оставьте поле пустым для просмотра всех записей.");
                return;
            }
        }

        _objectId = objectId;
        _objectGuid = objectGuid;
        await LoadPageAsync(0);
    }

    private async Task LoadPageAsync(int offset)
    {
        if (_settings is null)
        {
            ShowError("Сначала укажите настройки подключения.");
            return;
        }

        SetBusy(true);
        ClearRecords();
        _hasNext = false;
        PageText = "";
        ShowStatus("Загружаются записи…");
        using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _operation = operation;

        try
        {
            var records = await _database.ReadPageAsync(_settings, offset, _objectId, _objectGuid, operation.Token);

            if (_isClosed)
                return;

            _offset = offset;
            _hasNext = records.Count > PostgresCoordinateReader.PageSize;
            var page = records.Take(PostgresCoordinateReader.PageSize).ToArray();
            Records = page;
            PageText = $"Страница {_offset / PostgresCoordinateReader.PageSize + 1}";
            ShowStatus(page.Length == 0 ? "Записи не найдены." : $"Записей на странице: {page.Length}. Выберите запись для загрузки матрицы.");
        }
        catch (OperationCanceledException)
        {
            if (!_isClosed)
                ShowError("Превышено время ожидания загрузки записей.");
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

    private void SelectRecord()
    {
        if (SelectedRecord is not CoordinateRecord record)
            return;

        try
        {
            SelectedPlacement = record.GetValidatedPlacement();
            CloseRequested?.Invoke();
        }
        catch (FormatException exception)
        {
            ShowError($"Матрица выбранной записи некорректна: {exception.Message}");
        }
    }

    private bool CanInteract() => IsIdle && !_isClosed;

    private void ClearRecords()
    {
        SelectedRecord = null;
        Records = Array.Empty<CoordinateRecord>();
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        OnPropertyChanged(nameof(IsIdle));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        SettingsCommand.NotifyCanExecuteChanged();
        SearchCommand.NotifyCanExecuteChanged();
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        SelectCommand.NotifyCanExecuteChanged();
    }

    private void ShowError(string message)
    {
        ShowStatus(message, "Firebrick");
    }
}
