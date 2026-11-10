using System.Globalization;
using System.IO;
using CoordinatesTinyApp.Infrastructure;
using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;

namespace CoordinatesTinyApp.ViewModels;

internal sealed class MatrixEditorViewModel : StatusViewModel
{
    private const int MatrixSize = 4;
    private readonly IUserDialogService _dialogs;
    private TransformationMap? _currentMap;
    private string _inputXml = "";
    private string _translationX = "";
    private string _translationY = "";
    private string _translationZ = "";
    private bool _isEditorEnabled;

    public MatrixEditorViewModel(IUserDialogService dialogs)
    {
        _dialogs = dialogs;
        Cells = Enumerable.Range(0, MatrixSize * MatrixSize).Select(_ => new MatrixCellViewModel()).ToArray();
        LoadStringCommand = new RelayCommand(LoadString);
        OpenFileCommand = new RelayCommand(OpenFile);
        OpenDatabaseCommand = new RelayCommand(OpenDatabase);
        ApplyChangesCommand = new RelayCommand(ApplyChanges, () => IsEditorEnabled);
        ResetChangesCommand = new RelayCommand(ResetChanges, () => IsEditorEnabled);
        SaveFileCommand = new RelayCommand(SaveFile, () => IsEditorEnabled);
        ShowStatus("Ожидается загрузка координат.", "#4B5563");
    }

    public event Action<int>? FocusRequested;

    public IReadOnlyList<MatrixCellViewModel> Cells { get; }

    public RelayCommand LoadStringCommand { get; }

    public RelayCommand OpenFileCommand { get; }

    public RelayCommand OpenDatabaseCommand { get; }

    public RelayCommand ApplyChangesCommand { get; }

    public RelayCommand ResetChangesCommand { get; }

    public RelayCommand SaveFileCommand { get; }

    public string InputXml
    {
        get => _inputXml;
        set => SetProperty(ref _inputXml, value);
    }

    public string TranslationX
    {
        get => _translationX;
        private set => SetProperty(ref _translationX, value);
    }

    public string TranslationY
    {
        get => _translationY;
        private set => SetProperty(ref _translationY, value);
    }

    public string TranslationZ
    {
        get => _translationZ;
        private set => SetProperty(ref _translationZ, value);
    }

    public bool IsEditorEnabled
    {
        get => _isEditorEnabled;
        private set
        {
            if (SetProperty(ref _isEditorEnabled, value))
            {
                ApplyChangesCommand.NotifyCanExecuteChanged();
                ResetChangesCommand.NotifyCanExecuteChanged();
                SaveFileCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private void LoadString()
    {
        LoadMap(InputXml);
    }

    private void OpenFile()
    {
        string? path = _dialogs.SelectOpenFile("Открыть координаты");

        if (path is null)
            return;

        try
        {
            string xml = TransformationMapFileReader.ReadXml(path);
            InputXml = xml;
            LoadMap(xml);
        }
        catch (FormatException exception)
        {
            ShowError(exception.Message);
        }
        catch (IOException exception)
        {
            ShowError($"Не удалось прочитать файл: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowError($"Нет доступа к файлу: {exception.Message}");
        }
    }

    private void OpenDatabase()
    {
        string? xml = _dialogs.SelectPlacement();

        if (xml is null)
            return;

        InputXml = xml;
        LoadMap(xml);
    }

    private void ApplyChanges()
    {
        var map = ReadMapFromEditor();

        if (map is null)
            return;

        SetCurrentMap(map);
        ShowStatus("Изменения применены. Теперь матрицу можно сохранить в XML.", "ForestGreen");
    }

    private void ResetChanges()
    {
        if (_currentMap is null)
            return;

        PopulateEditor(_currentMap);
        ShowStatus("Значения редактора восстановлены.", "DarkSlateGray");
    }

    private void SaveFile()
    {
        var map = ReadMapFromEditor();

        if (map is null)
            return;

        string? path = _dialogs.SelectSaveFile();

        if (path is null)
            return;

        try
        {
            var bytes = TransformationMapXmlSerializer.SerializeToUtf8(map);
            File.WriteAllBytes(path, bytes);

            TransformationMapXmlSerializer.Parse(File.ReadAllText(path));
            SetCurrentMap(map);
            ShowStatus($"Проверенный XML сохранён: {path}", "ForestGreen");
        }
        catch (FormatException exception)
        {
            ShowError(exception.Message);
        }
        catch (IOException exception)
        {
            ShowError($"Не удалось сохранить файл: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowError($"Нет доступа для сохранения файла: {exception.Message}");
        }
    }

    private void LoadMap(string xml)
    {
        try
        {
            var map = TransformationMapXmlSerializer.Parse(xml);
            SetCurrentMap(map);
            ShowStatus("Матрица успешно загружена. Все 16 значений прошли проверку.", "ForestGreen");
        }
        catch (FormatException exception)
        {
            ShowError(exception.Message);
        }
    }

    private void SetCurrentMap(TransformationMap map)
    {
        _currentMap = map;
        PopulateEditor(map);
        IsEditorEnabled = true;
    }

    private void PopulateEditor(TransformationMap map)
    {
        for (int row = 0; row < MatrixSize; row++)
        {
            for (int column = 0; column < MatrixSize; column++)
                Cells[row * MatrixSize + column].Text = FormatValue(map[row, column]);
        }

        TranslationX = FormatValue(map.TranslationX);
        TranslationY = FormatValue(map.TranslationY);
        TranslationZ = FormatValue(map.TranslationZ);
    }

    private TransformationMap? ReadMapFromEditor()
    {
        var values = new double[MatrixSize, MatrixSize];

        for (int row = 0; row < MatrixSize; row++)
        {
            for (int column = 0; column < MatrixSize; column++)
            {
                var cell = Cells[row * MatrixSize + column];

                if (!TryParseEditorValue(cell.Text, out double value))
                {
                    string attributeName = $"m{row + 1}{column + 1}";
                    ShowError($"{attributeName} должен содержать конечное число без разделителей групп разрядов.");
                    FocusRequested?.Invoke(row * MatrixSize + column);
                    return null;
                }

                values[row, column] = value;
            }
        }

        return new TransformationMap(values);
    }

    private static bool TryParseEditorValue(string text, out double value)
    {
        bool parsed = Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            || Double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);

        return parsed && Double.IsFinite(value);
    }

    private static string FormatValue(double value)
    {
        return value.ToString("R", CultureInfo.CurrentCulture);
    }

    private void ShowError(string message)
    {
        ShowStatus(message, "Firebrick");
        _dialogs.ShowWarning(message);
    }
}
