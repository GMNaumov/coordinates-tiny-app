using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;

namespace CoordinatesTinyApp.Views;

public partial class DatabaseRecordPickerWindow : Window
{
    private readonly DatabaseSettingsStore _store;
    private DatabaseConnectionSettings? _settings;
    private CancellationTokenSource? _operation;
    private int _offset;
    private int? _objectId;
    private Guid? _objectGuid;
    private bool _hasNext;
    private bool _isClosed;

    internal DatabaseRecordPickerWindow(DatabaseSettingsStore store)
    {
        InitializeComponent();
        _store = store;
    }

    public string? SelectedPlacement { get; private set; }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            _settings = _store.Load();

            if (_settings is null)
            {
                var dialog = new DatabaseSettingsWindow(_store) { Owner = this };

                if (dialog.ShowDialog() != true)
                {
                    ShowStatus("Для загрузки записей укажите настройки подключения.", false);
                    return;
                }

                _settings = _store.Load();
            }

            ConnectionTextBlock.Text = $"{_settings!.Server}:{_settings.Port} / {_settings.Database} — dbo.Coordinates";
            await LoadPageAsync(0);
        }
        catch (Exception exception) when (DatabaseErrorMessages.IsExpected(exception))
        {
            _settings = null;
            ShowStatus(DatabaseErrorMessages.GetMessage(exception) + " Откройте настройки подключения.", true);
        }
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DatabaseSettingsWindow(_store) { Owner = this };

        if (dialog.ShowDialog() != true)
            return;

        RecordsDataGrid.ItemsSource = null;
        _offset = 0;
        _hasNext = false;
        _objectId = null;
        _objectGuid = null;
        SearchTextBox.Clear();
        SetBusy(false);
        await LoadSettingsAsync();
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        string search = SearchTextBox.Text.Trim();
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
                ShowStatus("Введите целочисленный ID объекта или GUID. Оставьте поле пустым для просмотра всех записей.", true);
                return;
            }
        }

        _objectId = objectId;
        _objectGuid = objectGuid;
        await LoadPageAsync(0);
    }

    private async void PreviousButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadPageAsync(Math.Max(0, _offset - PostgresCoordinateReader.PageSize));
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadPageAsync(_offset + PostgresCoordinateReader.PageSize);
    }

    private async Task LoadPageAsync(int offset)
    {
        if (_settings is null)
        {
            ShowStatus("Сначала укажите настройки подключения.", true);
            return;
        }

        SetBusy(true);
        RecordsDataGrid.ItemsSource = null;
        _hasNext = false;
        PageTextBlock.Text = "";
        ShowStatus("Загружаются записи…", false);
        using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _operation = operation;

        try
        {
            var records = await PostgresCoordinateReader.ReadPageAsync(_settings, offset, _objectId, _objectGuid, operation.Token);

            if (_isClosed)
                return;

            _offset = offset;
            _hasNext = records.Count > PostgresCoordinateReader.PageSize;
            var page = records.Take(PostgresCoordinateReader.PageSize).ToArray();
            RecordsDataGrid.ItemsSource = page;
            PageTextBlock.Text = $"Страница {_offset / PostgresCoordinateReader.PageSize + 1}";
            ShowStatus(page.Length == 0 ? "Записи не найдены." : $"Записей на странице: {page.Length}. Выберите запись для загрузки матрицы.", false);
        }
        catch (OperationCanceledException)
        {
            if (!_isClosed)
                ShowStatus("Превышено время ожидания загрузки записей.", true);
        }
        catch (Exception exception) when (DatabaseErrorMessages.IsExpected(exception))
        {
            if (!_isClosed)
                ShowStatus(DatabaseErrorMessages.GetMessage(exception), true);
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
        SettingsButton.IsEnabled = !isBusy;
        SearchPanel.IsEnabled = !isBusy;
        RecordsDataGrid.IsEnabled = !isBusy;
        PreviousButton.IsEnabled = !isBusy && _offset > 0;
        NextButton.IsEnabled = !isBusy && _hasNext && _offset <= Int32.MaxValue - PostgresCoordinateReader.PageSize;
        SelectButton.IsEnabled = !isBusy && RecordsDataGrid.SelectedItem is CoordinateRecord;
    }

    private void RecordsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectButton.IsEnabled = _operation is null && RecordsDataGrid.SelectedItem is CoordinateRecord;
    }

    private void RecordsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Двойной щелчок по заголовку или пустому месту не должен подтверждать прежнее выделение.
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(RecordsDataGrid, source) is DataGridRow)
            SelectRecord();
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        SelectRecord();
    }

    private void SelectRecord()
    {
        if (_operation is not null || RecordsDataGrid.SelectedItem is not CoordinateRecord record)
            return;

        try
        {
            SelectedPlacement = record.GetValidatedPlacement();
            DialogResult = true;
        }
        catch (FormatException exception)
        {
            ShowStatus($"Матрица выбранной записи некорректна: {exception.Message}", true);
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _operation?.Cancel();
        _settings = null;
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = isError ? Brushes.Firebrick : Brushes.DarkSlateGray;
    }
}
