using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;

namespace CoordinatesTinyApp.Views;

public partial class DatabaseSettingsWindow : Window
{
    private readonly DatabaseSettingsStore _store;
    private CancellationTokenSource? _operation;
    private bool _isClosed;

    internal DatabaseSettingsWindow(DatabaseSettingsStore store)
    {
        InitializeComponent();
        _store = store;
        SettingsPathTextBlock.Text = $"Файл настроек: {store.FilePath}";
        var settings = new DatabaseConnectionSettings();

        try
        {
            settings = store.Load() ?? settings;
        }
        catch (Exception exception) when (DatabaseErrorMessages.IsExpected(exception))
        {
            ShowStatus(DatabaseErrorMessages.GetMessage(exception) + " Заполните поля и сохраните настройки заново.", true);
        }

        ServerTextBox.Text = settings.Server;
        PortTextBox.Text = settings.Port.ToString(CultureInfo.InvariantCulture);
        DatabaseTextBox.Text = settings.Database;
        UsernameTextBox.Text = settings.Username;
        DatabasePasswordBox.Password = settings.Password;
    }

    private DatabaseConnectionSettings ReadSettings()
    {
        if (!Int32.TryParse(PortTextBox.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int port))
            throw new FormatException("Порт должен быть целым числом от 1 до 65535.");

        var settings = new DatabaseConnectionSettings
        {
            Server = ServerTextBox.Text.Trim(),
            Port = port,
            Database = DatabaseTextBox.Text.Trim(),
            Username = UsernameTextBox.Text.Trim(),
            Password = DatabasePasswordBox.Password
        };
        settings.Validate();
        return settings;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _store.Save(ReadSettings());
            DialogResult = true;
        }
        catch (FormatException exception)
        {
            ShowStatus(exception.Message, true);
        }
        catch (Exception exception) when (DatabaseErrorMessages.IsExpected(exception))
        {
            ShowStatus(DatabaseErrorMessages.GetMessage(exception), true);
        }
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = ReadSettings();
            FieldsGrid.IsEnabled = false;
            TestButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            ShowStatus("Проверяется подключение…", false);
            using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _operation = operation;
            await PostgresCoordinateReader.TestConnectionAsync(settings, operation.Token);

            if (!_isClosed)
                ShowStatus("Подключение успешно. Для сохранения настроек нажмите «Сохранить».", false);
        }
        catch (OperationCanceledException)
        {
            if (!_isClosed)
                ShowStatus("Превышено время ожидания подключения.", true);
        }
        catch (FormatException exception)
        {
            ShowStatus(exception.Message, true);
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
            {
                FieldsGrid.IsEnabled = true;
                TestButton.IsEnabled = true;
                SaveButton.IsEnabled = true;
            }
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _isClosed = true;
        _operation?.Cancel();
        DatabasePasswordBox.Clear();
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = isError ? Brushes.Firebrick : Brushes.DarkSlateGray;
    }
}
