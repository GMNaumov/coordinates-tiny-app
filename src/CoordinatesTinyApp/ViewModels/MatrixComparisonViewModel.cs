using System.IO;
using CoordinatesTinyApp.Infrastructure;
using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;

namespace CoordinatesTinyApp.ViewModels;

internal sealed class MatrixComparisonViewModel : StatusViewModel
{
    private readonly IUserDialogService _dialogs;
    private string _firstInputXml = "";
    private string _secondInputXml = "";
    private IReadOnlyList<TransformationMapDifference> _differences = Array.Empty<TransformationMapDifference>();

    public MatrixComparisonViewModel(IUserDialogService dialogs)
    {
        _dialogs = dialogs;
        OpenFirstFileCommand = new RelayCommand(OpenFirstFile);
        OpenSecondFileCommand = new RelayCommand(OpenSecondFile);
        OpenFirstDatabaseCommand = new RelayCommand(OpenFirstDatabase);
        OpenSecondDatabaseCommand = new RelayCommand(OpenSecondDatabase);
        CompareCommand = new RelayCommand(Compare);
        ShowStatus("Введите или загрузите две матрицы для сравнения.", "#4B5563");
    }

    public event Action<bool>? FocusRequested;

    public RelayCommand OpenFirstFileCommand { get; }

    public RelayCommand OpenSecondFileCommand { get; }

    public RelayCommand OpenFirstDatabaseCommand { get; }

    public RelayCommand OpenSecondDatabaseCommand { get; }

    public RelayCommand CompareCommand { get; }

    public string FirstInputXml
    {
        get => _firstInputXml;
        set
        {
            if (SetProperty(ref _firstInputXml, value))
                InvalidateResult();
        }
    }

    public string SecondInputXml
    {
        get => _secondInputXml;
        set
        {
            if (SetProperty(ref _secondInputXml, value))
                InvalidateResult();
        }
    }

    public IReadOnlyList<TransformationMapDifference> Differences
    {
        get => _differences;
        private set
        {
            if (SetProperty(ref _differences, value))
                OnPropertyChanged(nameof(HasDifferences));
        }
    }

    public bool HasDifferences => Differences.Count > 0;

    private void InvalidateResult()
    {
        // Результат относится только к тем исходным данным, по которым он был рассчитан.
        ClearResult();
        ShowStatus("Исходные данные изменены. Нажмите «Сравнить матрицы».");
    }

    private void SetInput(bool isFirst, string xml)
    {
        if (isFirst)
        {
            FirstInputXml = xml;
        }
        else
        {
            SecondInputXml = xml;
        }
    }

    private void OpenFirstFile()
    {
        OpenFile(true, "Первая матрица");
    }

    private void OpenSecondFile()
    {
        OpenFile(false, "Вторая матрица");
    }

    private void OpenFirstDatabase()
    {
        OpenDatabase(true, "Первая матрица");
    }

    private void OpenSecondDatabase()
    {
        OpenDatabase(false, "Вторая матрица");
    }

    private void OpenDatabase(bool isFirst, string matrixName)
    {
        string? xml = _dialogs.SelectPlacement();

        if (xml is null)
            return;

        SetInput(isFirst, xml);
        ClearResult();
        ShowStatus($"{matrixName} загружена из PostgreSQL. Нажмите «Сравнить матрицы».", "DarkSlateGray");
    }

    private void OpenFile(bool isFirst, string matrixName)
    {
        string? path = _dialogs.SelectOpenFile($"{matrixName}: открыть координаты");

        if (path is null)
            return;

        try
        {
            string xml = TransformationMapFileReader.ReadXml(path);
            TransformationMapXmlSerializer.Parse(xml);
            SetInput(isFirst, xml);
            ClearResult();
            ShowStatus($"{matrixName} загружена: {path}. Нажмите «Сравнить матрицы».", "DarkSlateGray");
        }
        catch (FormatException exception)
        {
            ShowError($"{matrixName}: {exception.Message}");
        }
        catch (IOException exception)
        {
            ShowError($"{matrixName}: не удалось прочитать файл: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            ShowError($"{matrixName}: нет доступа к файлу: {exception.Message}");
        }
    }

    private void Compare()
    {
        ClearResult();
        var firstMap = ReadMap(true, "Первая матрица");

        if (firstMap is null)
            return;

        var secondMap = ReadMap(false, "Вторая матрица");

        if (secondMap is null)
            return;

        var differences = TransformationMapComparer.Compare(firstMap, secondMap);

        if (differences.Count == 0)
        {
            ShowStatus("Матрицы идентичны: все 16 числовых значений совпадают.", "ForestGreen");
        }
        else
        {
            Differences = differences;

            ShowStatus($"Матрицы различаются. Количество отличающихся параметров: {differences.Count} из 16.", "DarkSlateGray");
        }
    }

    private TransformationMap? ReadMap(bool isFirst, string matrixName)
    {
        try
        {
            return TransformationMapXmlSerializer.Parse(isFirst ? FirstInputXml : SecondInputXml);
        }
        catch (FormatException exception)
        {
            ShowError($"{matrixName}: {exception.Message}");
            FocusRequested?.Invoke(isFirst);
            return null;
        }
    }

    private void ClearResult()
    {
        Differences = Array.Empty<TransformationMapDifference>();
    }

    private void ShowError(string message)
    {
        ClearResult();
        ShowStatus(message, "Firebrick");
    }
}
