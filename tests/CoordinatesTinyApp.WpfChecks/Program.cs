using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;
using CoordinatesTinyApp.ViewModels;
using CoordinatesTinyApp.Views;

namespace CoordinatesTinyApp.WpfChecks;

// Проверяем настоящие XAML-привязки в STA-потоке без открытия окон и без рабочей БД.
internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var app = new App();
        app.InitializeComponent();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        using var errors = new BindingErrorListener();
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(errors);
        Action[] checks =
        {
            MainWindowComposesBothTabs,
            EditorBindingsUpdateInBothDirections,
            ComparisonBindingsInvalidateResults,
            SettingsBindingsAndPasswordBridgeWork,
            PickerBindingsUpdateSelectionAndCommands
        };

        try
        {
            foreach (var check in checks)
            {
                check();
                FlushBindings();
                Require(errors.Messages.Length == 0, $"WPF binding errors: {errors.Messages}");
                Console.WriteLine($"PASS {check.Method.Name}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL WPF checks: {exception}");
            return 1;
        }
        finally
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(errors);
            app.Shutdown();
        }
    }

    private static void MainWindowComposesBothTabs()
    {
        var window = new MainWindow();

        try
        {
            var viewModel = (MainWindowViewModel)window.DataContext;
            var tabs = FindLogical<TabControl>(window).Single();
            FlushBindings();
            var editor = (MatrixEditorView)((TabItem)tabs.Items[0]).Content;
            var comparison = (MatrixComparisonView)((TabItem)tabs.Items[1]).Content;
            Require(ReferenceEquals(editor.DataContext, viewModel.Editor), "Editor receives its own ViewModel.");
            tabs.SelectedIndex = 1;
            FlushBindings();
            Require(ReferenceEquals(comparison.DataContext, viewModel.Comparison), "Comparison receives its own ViewModel.");
            var settingsButton = FindLogical<Button>(window).Single(button => button.Content as string == "Настройки подключения к БД…");
            Require(ReferenceEquals(settingsButton.Command, viewModel.DatabaseSettingsCommand), "Settings button uses the main command.");
        }
        finally
        {
            window.Close();
        }
    }

    private static void EditorBindingsUpdateInBothDirections()
    {
        var viewModel = new MatrixEditorViewModel(new FakeDialogs());
        var view = new MatrixEditorView { DataContext = viewModel };
        FlushBindings();
        var input = (TextBox)view.FindName("InputTextBox");
        input.SetCurrentValue(TextBox.TextProperty, CreateXml());
        FlushBindings();
        Require(viewModel.InputXml == input.Text && !SpellCheck.GetIsEnabled(input), "XML text updates the source and spell checking remains disabled.");
        viewModel.LoadStringCommand.Execute(null);
        FlushBindings();
        Require(((GroupBox)view.FindName("EditorGroupBox")).IsEnabled, "Loaded matrix enables the editor group.");

        for (int row = 1; row <= 4; row++)
        {
            for (int column = 1; column <= 4; column++)
            {
                var cell = (TextBox)view.FindName($"M{row}{column}TextBox");
                int index = (row - 1) * 4 + column - 1;
                Require(cell.Text == (row == column ? "1" : "0"), "Every matrix cell reads the correct row and column.");
                cell.SetCurrentValue(TextBox.TextProperty, (index + 1).ToString());
                FlushBindings();
                Require(viewModel.Cells[index].Text == cell.Text, "Every matrix cell updates its own source.");
            }
        }

        viewModel.ApplyChangesCommand.Execute(null);
        FlushBindings();
        var translations = FindLogical<TextBox>(view).Where(textBox => textBox.IsReadOnly).ToArray();
        Require(translations.Select(textBox => textBox.Text).SequenceEqual(new[] { "13", "14", "15" }), "Applied translations update all read-only displays.");
        viewModel.Cells[0].Text = "999";
        viewModel.ResetChangesCommand.Execute(null);
        FlushBindings();
        Require(((TextBox)view.FindName("M11TextBox")).Text == "1", "Reset updates the bound controls.");
    }

    private static void ComparisonBindingsInvalidateResults()
    {
        var viewModel = new MatrixComparisonViewModel(new FakeDialogs());
        var view = new MatrixComparisonView { DataContext = viewModel };
        var first = (TextBox)view.FindName("FirstInputTextBox");
        var second = (TextBox)view.FindName("SecondInputTextBox");
        FlushBindings();
        first.SetCurrentValue(TextBox.TextProperty, CreateXml());
        second.SetCurrentValue(TextBox.TextProperty, CreateXml().Replace("m41=\"0\"", "m41=\"2\""));
        FlushBindings();
        viewModel.CompareCommand.Execute(null);
        FlushBindings();
        var differences = (DataGrid)view.FindName("DifferencesDataGrid");
        Require(differences.Visibility == Visibility.Visible && differences.Items.Count == 1, "Comparison binds the differences and visibility.");
        first.SetCurrentValue(TextBox.TextProperty, "invalid");
        FlushBindings();
        Require(differences.Visibility == Visibility.Collapsed && differences.Items.Count == 0, "Typing invalidates displayed results immediately.");
        viewModel.CompareCommand.Execute(null);
        FlushBindings();
        var status = (TextBlock)view.FindName("ComparisonStatusTextBlock");
        Require(status.Text.StartsWith("Первая матрица:", StringComparison.Ordinal), "Validation errors update the same view.");
    }

    private static void SettingsBindingsAndPasswordBridgeWork()
    {
        var window = new DatabaseSettingsWindow(new FakeStore());

        try
        {
            var viewModel = (DatabaseSettingsViewModel)window.DataContext;
            FlushBindings();
            Require(((TextBox)window.FindName("ServerTextBox")).Text == "localhost", "Settings load into bound fields.");
            var password = (PasswordBox)window.FindName("DatabasePasswordBox");
            Require(password.Password == "synthetic", "PasswordBox initializes from loaded settings.");
            password.Password = "updated-synthetic";
            Require(viewModel.Password == password.Password, "PasswordBox changes reach the ViewModel.");
            ((TextBox)window.FindName("PortTextBox")).SetCurrentValue(TextBox.TextProperty, "bad");
            FlushBindings();
            viewModel.SaveCommand.Execute(null);
            FlushBindings();
            Require(viewModel.Port == "bad" && FindLogical<TextBlock>(window).Any(block => block.Text.StartsWith("Порт должен", StringComparison.Ordinal)), "Validation displays a bound error without closing.");
            window.Close();
            Require(viewModel.Password.Length == 0 && password.Password.Length == 0, "Closing clears both password copies.");
        }
        finally
        {
            window.Close();
        }
    }

    private static void PickerBindingsUpdateSelectionAndCommands()
    {
        var window = new DatabaseRecordPickerWindow(new FakeStore(), new FakeDatabase());

        try
        {
            var viewModel = (DatabaseRecordPickerViewModel)window.DataContext;
            viewModel.InitializeAsync().GetAwaiter().GetResult();
            FlushBindings();
            var records = (DataGrid)window.FindName("RecordsDataGrid");
            var select = (Button)window.FindName("SelectButton");
            Require(records.Items.Count == 1 && !select.IsEnabled, "Loaded records bind without auto-selecting a row.");
            records.SetCurrentValue(DataGrid.SelectedItemProperty, records.Items[0]);
            FlushBindings();
            Require(ReferenceEquals(viewModel.SelectedRecord, records.Items[0]) && select.IsEnabled, "Selection updates the ViewModel and enables the command.");
            viewModel.SelectedRecord = null;
            FlushBindings();
            Require(records.SelectedItem is null && !select.IsEnabled, "Clearing selection updates the control and command.");
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<T> FindLogical<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is T match)
                yield return match;

            if (child is DependencyObject dependencyObject)
            {
                foreach (var descendant in FindLogical<T>(dependencyObject))
                    yield return descendant;
            }
        }
    }

    private static void FlushBindings()
    {
        Dispatcher.CurrentDispatcher.Invoke(() =>
        {
        }, DispatcherPriority.ApplicationIdle);
    }

    private static string CreateXml()
    {
        var attributes = from row in Enumerable.Range(1, 4)
                         from column in Enumerable.Range(1, 4)
                         select $"m{row}{column}=\"{(row == column ? 1 : 0)}\"";
        return "<TransformationMap " + String.Join(" ", attributes) + "/>";
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class BindingErrorListener : TraceListener
    {
        public StringBuilder Messages { get; } = new();

        public override void Write(string? message) => Messages.Append(message);

        public override void WriteLine(string? message) => Messages.AppendLine(message);
    }

    private sealed class FakeStore : IDatabaseSettingsStore
    {
        public string FilePath => "synthetic-settings.json";

        public DatabaseConnectionSettings Load() => new() { Database = "db", Username = "user", Password = "synthetic" };

        public void Save(DatabaseConnectionSettings settings) => throw new InvalidOperationException("This check must not write settings.");
    }

    private sealed class FakeDialogs : IUserDialogService
    {
        public string? SelectOpenFile(string title) => null;

        public string? SelectSaveFile() => null;

        public string? SelectPlacement() => null;

        public bool ShowDatabaseSettings() => false;

        public void ShowWarning(string message) => throw new InvalidOperationException(message);
    }

    private sealed class FakeDatabase : ICoordinateDatabase
    {
        public Task TestConnectionAsync(DatabaseConnectionSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<CoordinateRecord>> ReadPageAsync(DatabaseConnectionSettings settings, int offset,
            int? objectId, Guid? objectGuid, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<CoordinateRecord>>(new[] { new CoordinateRecord { Placement = CreateXml() } });
        }
    }
}
