using System.Globalization;
using System.Text;
using CoordinatesTinyApp.Infrastructure;
using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;
using CoordinatesTinyApp.ViewModels;

namespace CoordinatesTinyApp.Checks;

internal static class ViewModelChecks
{
    public static void EditorPreservesLoadApplyAndResetBehavior()
    {
        var dialogs = new FakeDialogs();
        var editor = new MatrixEditorViewModel(dialogs);
        Require(!editor.IsEditorEnabled && !editor.ApplyChangesCommand.CanExecute(null), "Editor starts disabled.");
        editor.InputXml = CreateXml();
        editor.LoadStringCommand.Execute(null);
        Require(editor.IsEditorEnabled && editor.Cells.Count == 16, "Loading enables all cells.");
        editor.Cells[12].Text = "2";
        Require(editor.TranslationX == "0", "Translation shows the applied matrix until Apply.");
        editor.ResetChangesCommand.Execute(null);
        Require(editor.Cells[12].Text == "0", "Reset restores the loaded matrix.");
        editor.Cells[12].Text = "3";
        editor.ApplyChangesCommand.Execute(null);
        editor.Cells[12].Text = "4";
        editor.ResetChangesCommand.Execute(null);
        Require(editor.Cells[12].Text == "3" && editor.TranslationX == "3", "Apply replaces the reset baseline.");
        editor.InputXml = "<invalid />";
        editor.LoadStringCommand.Execute(null);
        Require(editor.Cells[12].Text == "3" && dialogs.Warnings.Count == 1, "Invalid load preserves the previous matrix.");
    }

    public static void EditorValidatesNumbersAndRequestsFocus()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var editor = new MatrixEditorViewModel(new FakeDialogs()) { InputXml = CreateXml() };
            editor.LoadStringCommand.Execute(null);
            editor.Cells[12].Text = "1,25";
            editor.Cells[13].Text = "2.5";
            editor.ApplyChangesCommand.Execute(null);
            Require(editor.TranslationX == "1,25" && editor.TranslationY == "2,5", "Both decimal separators remain supported.");
            int focusedIndex = -1;
            editor.FocusRequested += index => focusedIndex = index;

            foreach (string invalid in new[] { "NaN", "Infinity", "1e309", "1 000", "" })
            {
                editor.Cells[5].Text = invalid;
                editor.ApplyChangesCommand.Execute(null);
                Require(focusedIndex == 5 && editor.StatusColor == "Firebrick", "Invalid input identifies and focuses the first cell.");
                Require(editor.TranslationX == "1,25", "Validation failure does not apply changes.");
            }

            editor.ResetChangesCommand.Execute(null);
            Require(editor.Cells[5].Text == "1", "Validation failure leaves the reset baseline intact.");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    public static void EditorSaveAndCancellationPreserveCommitBoundary()
    {
        string directory = Path.Combine(Path.GetTempPath(), "coordinates-mvvm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var dialogs = new FakeDialogs();
            var editor = new MatrixEditorViewModel(dialogs) { InputXml = CreateXml() };
            editor.LoadStringCommand.Execute(null);
            editor.Cells[12].Text = "5";
            editor.SaveFileCommand.Execute(null);
            editor.ResetChangesCommand.Execute(null);
            Require(editor.Cells[12].Text == "0", "Cancelled save does not apply edits.");
            dialogs.SavePath = Path.Combine(directory, "matrix.xml");
            editor.Cells[12].Text = "1.2345678901234567";
            editor.SaveFileCommand.Execute(null);
            var bytes = File.ReadAllBytes(dialogs.SavePath);
            Require(!bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()), "Saved XML has no BOM.");
            var saved = TransformationMapXmlSerializer.Parse(Encoding.UTF8.GetString(bytes));
            Require(saved.TranslationX == 1.2345678901234567, "Saving preserves round-trip values.");
            editor.Cells[12].Text = "9";
            editor.ResetChangesCommand.Execute(null);
            Require(Double.Parse(editor.Cells[12].Text, CultureInfo.CurrentCulture) == saved.TranslationX, "Successful save applies the saved matrix.");
            dialogs.SavePath = Path.Combine(directory, "missing", "matrix.xml");
            editor.Cells[12].Text = "8";
            editor.SaveFileCommand.Execute(null);
            editor.ResetChangesCommand.Execute(null);
            Require(Double.Parse(editor.Cells[12].Text, CultureInfo.CurrentCulture) == saved.TranslationX, "Failed save preserves the previous baseline.");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    public static void ComparisonInvalidatesResultsAndIdentifiesInputErrors()
    {
        var comparison = new MatrixComparisonViewModel(new FakeDialogs())
        {
            FirstInputXml = CreateXml(),
            SecondInputXml = CreateXml("1")
        };
        var notifications = new List<string?>();
        comparison.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        comparison.CompareCommand.Execute(null);
        Require(comparison.HasDifferences && comparison.Differences.Single().ParameterName == "m41", "Comparison exposes the differing parameter.");
        Require(notifications.Contains(nameof(comparison.HasDifferences)), "Visibility is notified for binding.");
        comparison.SecondInputXml = CreateXml("2");
        Require(!comparison.HasDifferences && comparison.Differences.Count == 0, "Second input edits clear the result.");
        comparison.CompareCommand.Execute(null);
        comparison.FirstInputXml = CreateXml("2");
        Require(!comparison.HasDifferences, "First input edits clear the result.");
        comparison.CompareCommand.Execute(null);
        Require(comparison.StatusColor == "ForestGreen", "Equal matrices keep the identity message.");
        bool? focusFirst = null;
        comparison.FocusRequested += isFirst => focusFirst = isFirst;
        comparison.SecondInputXml = "bad";
        comparison.CompareCommand.Execute(null);
        Require(focusFirst == false && comparison.StatusMessage.StartsWith("Вторая матрица:", StringComparison.Ordinal), "Second input errors remain local to comparison.");
        comparison.FirstInputXml = "bad";
        comparison.CompareCommand.Execute(null);
        Require(focusFirst == true && comparison.StatusMessage.StartsWith("Первая матрица:", StringComparison.Ordinal), "First matrix is validated before the second.");
    }

    public static void FileAndDatabaseInputsRemainIndependent()
    {
        string path = Path.Combine(Path.GetTempPath(), "coordinates-mvvm-" + Guid.NewGuid().ToString("N") + ".xml");

        try
        {
            var dialogs = new FakeDialogs { OpenPath = path, Placement = CreateXml("7") };
            var main = new MainWindowViewModel(dialogs);
            main.Editor.InputXml = CreateXml();
            main.Editor.LoadStringCommand.Execute(null);
            main.Comparison.FirstInputXml = CreateXml();
            File.WriteAllText(path, "<invalid />");
            main.Comparison.OpenFirstFileCommand.Execute(null);
            Require(main.Comparison.FirstInputXml == CreateXml(), "Comparison rejects a file before replacing input.");
            main.Editor.OpenFileCommand.Execute(null);
            Require(main.Editor.InputXml == "<invalid />" && main.Editor.IsEditorEnabled, "Editor keeps the loaded file text while preserving the last valid matrix.");
            main.Comparison.OpenSecondDatabaseCommand.Execute(null);
            Require(main.Comparison.SecondInputXml == dialogs.Placement && main.Editor.Cells[12].Text == "0", "Database input affects only its target.");
            main.Editor.OpenDatabaseCommand.Execute(null);
            Require(main.Editor.TranslationX == "7" && main.Comparison.FirstInputXml == CreateXml(), "Editor database load leaves comparison independent.");
            dialogs.Placement = null;
            main.Editor.OpenDatabaseCommand.Execute(null);
            Require(main.Editor.TranslationX == "7", "Cancelled selection leaves input unchanged.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static void SettingsValidationAndRecoveryRemainAvailable()
    {
        var store = new FakeStore { LoadException = new FormatException("broken") };
        var settings = new DatabaseSettingsViewModel(store, new FakeDatabase());
        Require(settings.StatusColor == "Firebrick", "Corrupt settings are visible without preventing recovery.");
        PopulateSettings(settings);
        settings.Port = " 5432";
        settings.SaveCommand.Execute(null);
        Require(store.Saved is null, "Port keeps strict integer parsing.");
        settings.Port = "5432";
        bool closeRequested = false;
        settings.CloseRequested += () => closeRequested = true;
        settings.SaveCommand.Execute(null);
        Require(closeRequested && store.Saved is not null, "Valid settings save without contacting a server.");
        Require(store.Saved!.Server == "localhost" && store.Saved.Username == "user" && store.Saved.Password == " secret ", "Only non-password fields are trimmed.");
        settings.Close();
        Require(settings.Password.Length == 0 && !settings.SaveCommand.CanExecute(null), "Closing clears the password and disables commands.");
    }

    public static async Task RunAsync()
    {
        Func<Task>[] checks =
        {
            AsyncCommandPreventsReentryAndRecoversAfterFailure,
            SettingsTestBlocksEditingAndCancelsOnClose,
            SettingsTestReportsErrorsWithoutSecrets,
            PickerKeepsAppliedSearchAcrossPages,
            PickerValidatesSelectionAndSettingsChanges,
            PickerCancelsOnCloseAndIgnoresLateResults,
            PickerRecoversFromLoadErrors
        };

        foreach (var check in checks)
        {
            await check();
            Console.WriteLine($"PASS {check.Method.Name}");
        }
    }

    private static async Task AsyncCommandPreventsReentryAndRecoversAfterFailure()
    {
        var completion = new TaskCompletionSource();
        int calls = 0;
        var command = new AsyncRelayCommand(() =>
        {
            calls++;
            return completion.Task;
        });
        var task = command.ExecuteAsync();
        await command.ExecuteAsync();
        Require(calls == 1 && !command.CanExecute(null), "An active command cannot be started twice.");
        completion.SetException(new InvalidOperationException("test"));

        try
        {
            await task;
            throw new Exception("The original failure must remain observable.");
        }
        catch (InvalidOperationException)
        {
            Require(command.CanExecute(null), "Command availability is restored even after a failure.");
        }
    }

    private static async Task SettingsTestBlocksEditingAndCancelsOnClose()
    {
        var completion = new TaskCompletionSource();
        var database = new FakeDatabase { Test = (_, _) => completion.Task };
        var settings = new DatabaseSettingsViewModel(new FakeStore(), database);
        PopulateSettings(settings);
        var task = settings.TestConnectionCommand.ExecuteAsync();
        Require(!settings.IsIdle && !settings.SaveCommand.CanExecute(null), "Connection test blocks edits and save.");
        await settings.TestConnectionCommand.ExecuteAsync();
        Require(database.TestCalls == 1, "Connection test does not overlap.");
        string pendingStatus = settings.StatusMessage;
        settings.Close();
        Require(database.LastToken.IsCancellationRequested, "Closing cancels the active test.");
        completion.SetResult();
        await task;
        Require(settings.StatusMessage == pendingStatus && settings.Password.Length == 0, "Late success cannot update a closed dialog.");
    }

    private static async Task SettingsTestReportsErrorsWithoutSecrets()
    {
        var database = new FakeDatabase();
        var settings = new DatabaseSettingsViewModel(new FakeStore(), database);
        PopulateSettings(settings);
        await settings.TestConnectionCommand.ExecuteAsync();
        Require(settings.IsIdle && settings.StatusMessage.StartsWith("Подключение успешно", StringComparison.Ordinal), "Success re-enables the dialog.");
        database.Test = (_, _) => Task.FromException(new IOException("password= secret "));
        await settings.TestConnectionCommand.ExecuteAsync();
        Require(settings.IsIdle && settings.StatusColor == "Firebrick" && !settings.StatusMessage.Contains("secret", StringComparison.Ordinal), "Expected errors use safe messages.");
        database.Test = (_, _) => Task.FromCanceled(new CancellationToken(true));
        await settings.TestConnectionCommand.ExecuteAsync();
        Require(settings.StatusMessage.Contains("время ожидания", StringComparison.Ordinal) && settings.SaveCommand.CanExecute(null), "Timeout restores command availability.");
    }

    private static async Task PickerKeepsAppliedSearchAcrossPages()
    {
        var database = new FakeDatabase
        {
            Read = (_, offset, _, _, _) => Task.FromResult<IReadOnlyList<CoordinateRecord>>(
                Enumerable.Range(offset, offset == 0 ? 101 : 1).Select(id => new CoordinateRecord { ObjectId = id }).ToArray())
        };
        var picker = new DatabaseRecordPickerViewModel(new FakeStore { Settings = CreateSettings() }, database, new FakeDialogs());
        await picker.InitializeAsync();
        Require(picker.Records.Count == 100 && picker.NextCommand.CanExecute(null) && !picker.PreviousCommand.CanExecute(null), "Page size and sentinel are preserved.");
        picker.SearchText = "42";
        Require(database.ReadCalls == 1, "Typing does not execute search.");
        await picker.SearchCommand.ExecuteAsync();
        Require(database.LastObjectId == 42 && database.LastOffset == 0, "Search applies the exact ID and resets paging.");
        picker.SearchText = "43";
        await picker.NextCommand.ExecuteAsync();
        Require(database.LastObjectId == 42 && database.LastOffset == 100, "Paging uses applied search, not pending input.");
        Require(!picker.NextCommand.CanExecute(null) && picker.PreviousCommand.CanExecute(null), "Last page disables Next.");
        await picker.PreviousCommand.ExecuteAsync();
        Require(database.LastOffset == 0, "Previous returns to the first page.");
        var guid = Guid.NewGuid();
        picker.SearchText = guid.ToString();
        await picker.SearchCommand.ExecuteAsync();
        Require(database.LastObjectGuid == guid && database.LastObjectId is null, "GUID replaces the applied ID filter.");
        int calls = database.ReadCalls;
        picker.SearchText = "invalid";
        await picker.SearchCommand.ExecuteAsync();
        Require(database.ReadCalls == calls && picker.Records.Count == 100, "Invalid search preserves the current page.");
        picker.SearchText = "";
        await picker.SearchCommand.ExecuteAsync();
        Require(database.LastObjectId is null && database.LastObjectGuid is null, "Empty search clears both filters.");
    }

    private static async Task PickerValidatesSelectionAndSettingsChanges()
    {
        var store = new FakeStore();
        var database = new FakeDatabase();
        var dialogs = new FakeDialogs();
        var picker = new DatabaseRecordPickerViewModel(store, database, dialogs);
        await picker.InitializeAsync();
        Require(database.ReadCalls == 0 && dialogs.SettingsCalls == 1, "Missing settings open the dialog and allow cancellation.");
        dialogs.SettingsResult = true;
        dialogs.OnSettings = () => store.Settings = CreateSettings();
        await picker.SettingsCommand.ExecuteAsync();
        Require(database.ReadCalls == 1, "Saving settings loads the first page.");
        bool closed = false;
        picker.CloseRequested += () => closed = true;
        picker.SelectedRecord = new CoordinateRecord { Placement = null };
        picker.SelectCommand.Execute(null);
        Require(!closed && picker.SelectedPlacement is null && picker.StatusColor == "Firebrick", "NULL selection does not close the dialog.");
        picker.SelectedRecord = new CoordinateRecord { Placement = "invalid" };
        picker.SelectCommand.Execute(null);
        Require(!closed, "Invalid XML cannot be selected.");
        picker.SelectedRecord = new CoordinateRecord { Placement = CreateXml() };
        picker.SelectCommand.Execute(null);
        Require(closed && picker.SelectedPlacement == CreateXml(), "Valid selection requests dialog completion.");
        picker.SearchText = "123";
        await picker.SearchCommand.ExecuteAsync();
        await picker.SettingsCommand.ExecuteAsync();
        Require(picker.SearchText == "" && database.LastObjectId is null && !picker.SelectCommand.CanExecute(null), "Settings changes reset filters and selection.");
    }

    private static async Task PickerCancelsOnCloseAndIgnoresLateResults()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<CoordinateRecord>>();
        var database = new FakeDatabase { Read = (_, _, _, _, _) => completion.Task };
        var picker = new DatabaseRecordPickerViewModel(new FakeStore { Settings = CreateSettings() }, database, new FakeDialogs());
        var task = picker.InitializeAsync();
        Require(!picker.IsIdle && !picker.SearchCommand.CanExecute(null) && !picker.SettingsCommand.CanExecute(null), "Loading blocks all competing operations.");
        string pendingStatus = picker.StatusMessage;
        picker.Close();
        Require(database.LastToken.IsCancellationRequested, "Closing cancels the active read.");
        completion.SetResult(new[] { new CoordinateRecord { Placement = CreateXml() } });
        await task;
        Require(picker.Records.Count == 0 && picker.StatusMessage == pendingStatus && !picker.SelectCommand.CanExecute(null), "Late results cannot populate a closed picker.");
    }

    private static async Task PickerRecoversFromLoadErrors()
    {
        var database = new FakeDatabase
        {
            Read = (_, _, _, _, _) => Task.FromException<IReadOnlyList<CoordinateRecord>>(new IOException("secret"))
        };
        var picker = new DatabaseRecordPickerViewModel(new FakeStore { Settings = CreateSettings() }, database, new FakeDialogs());
        await picker.InitializeAsync();
        Require(picker.IsIdle && picker.StatusColor == "Firebrick" && !picker.StatusMessage.Contains("secret", StringComparison.Ordinal), "Read failure restores controls without exposing details.");
        database.Read = (_, _, _, _, _) => Task.FromCanceled<IReadOnlyList<CoordinateRecord>>(new CancellationToken(true));
        await picker.SearchCommand.ExecuteAsync();
        Require(picker.IsIdle && picker.StatusMessage.Contains("время ожидания", StringComparison.Ordinal), "Read timeout remains visible and retryable.");
        database.Read = (_, _, _, _, _) => Task.FromResult<IReadOnlyList<CoordinateRecord>>(Array.Empty<CoordinateRecord>());
        await picker.SearchCommand.ExecuteAsync();
        Require(picker.PageText == "Страница 1" && picker.StatusMessage == "Записи не найдены.", "Retry after failure can display an empty page.");
    }

    private static string CreateXml(string translationX = "0")
    {
        var attributes = from row in Enumerable.Range(1, 4)
                         from column in Enumerable.Range(1, 4)
                         let value = row == 4 && column == 1 ? translationX : row == column ? "1" : "0"
                         select $"m{row}{column}=\"{value}\"";
        return "<TransformationMap " + String.Join(" ", attributes) + "/>";
    }

    private static DatabaseConnectionSettings CreateSettings()
    {
        return new DatabaseConnectionSettings { Server = "localhost", Database = "db", Username = "user", Password = "secret" };
    }

    private static void PopulateSettings(DatabaseSettingsViewModel settings)
    {
        settings.Server = " localhost ";
        settings.Port = "5432";
        settings.DatabaseName = " db ";
        settings.Username = " user ";
        settings.Password = " secret ";
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FakeDialogs : IUserDialogService
    {
        public string? OpenPath { get; set; }

        public string? SavePath { get; set; }

        public string? Placement { get; set; }

        public bool SettingsResult { get; set; }

        public int SettingsCalls { get; private set; }

        public Action? OnSettings { get; set; }

        public List<string> Warnings { get; } = new();

        public string? SelectOpenFile(string title) => OpenPath;

        public string? SelectSaveFile() => SavePath;

        public string? SelectPlacement() => Placement;

        public bool ShowDatabaseSettings()
        {
            SettingsCalls++;
            OnSettings?.Invoke();
            return SettingsResult;
        }

        public void ShowWarning(string message) => Warnings.Add(message);
    }

    private sealed class FakeStore : IDatabaseSettingsStore
    {
        public string FilePath => "fake-settings.json";

        public DatabaseConnectionSettings? Settings { get; set; }

        public Exception? LoadException { get; set; }

        public DatabaseConnectionSettings? Saved { get; private set; }

        public DatabaseConnectionSettings? Load()
        {
            if (LoadException is not null)
                throw LoadException;

            return Settings;
        }

        public void Save(DatabaseConnectionSettings settings)
        {
            Saved = settings;
        }
    }

    private sealed class FakeDatabase : ICoordinateDatabase
    {
        public Func<DatabaseConnectionSettings, CancellationToken, Task> Test { get; set; } = (_, _) => Task.CompletedTask;

        public Func<DatabaseConnectionSettings, int, int?, Guid?, CancellationToken, Task<IReadOnlyList<CoordinateRecord>>> Read { get; set; }
            = (_, _, _, _, _) => Task.FromResult<IReadOnlyList<CoordinateRecord>>(Array.Empty<CoordinateRecord>());

        public int TestCalls { get; private set; }

        public int ReadCalls { get; private set; }

        public int LastOffset { get; private set; }

        public int? LastObjectId { get; private set; }

        public Guid? LastObjectGuid { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Task TestConnectionAsync(DatabaseConnectionSettings settings, CancellationToken cancellationToken)
        {
            TestCalls++;
            LastToken = cancellationToken;
            return Test(settings, cancellationToken);
        }

        public Task<IReadOnlyList<CoordinateRecord>> ReadPageAsync(DatabaseConnectionSettings settings, int offset,
            int? objectId, Guid? objectGuid, CancellationToken cancellationToken)
        {
            ReadCalls++;
            LastOffset = offset;
            LastObjectId = objectId;
            LastObjectGuid = objectGuid;
            LastToken = cancellationToken;
            return Read(settings, offset, objectId, objectGuid, cancellationToken);
        }
    }
}
