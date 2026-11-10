using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;
using Microsoft.Win32;

namespace CoordinatesTinyApp;

public partial class MainWindow : Window
{
    private const int MatrixSize = 4;

    private readonly TextBox[,] _cellTextBoxes;
    private TransformationMap? _currentMap;

    public MainWindow()
    {
        InitializeComponent();

        _cellTextBoxes = new TextBox[MatrixSize, MatrixSize]
        {
            { M11TextBox, M12TextBox, M13TextBox, M14TextBox },
            { M21TextBox, M22TextBox, M23TextBox, M24TextBox },
            { M31TextBox, M32TextBox, M33TextBox, M34TextBox },
            { M41TextBox, M42TextBox, M43TextBox, M44TextBox }
        };
    }

    private void LoadStringButton_Click(object sender, RoutedEventArgs e)
    {
        LoadMap(InputTextBox.Text);
    }

    private void OpenFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Открыть координаты",
            Filter = "XML-файлы (*.xml)|*.xml|Текстовые файлы (*.txt)|*.txt",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            string xml = TransformationMapFileReader.ReadXml(dialog.FileName);
            InputTextBox.Text = xml;
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

    private void ApplyChangesButton_Click(object sender, RoutedEventArgs e)
    {
        var map = ReadMapFromEditor();

        if (map is null)
            return;

        SetCurrentMap(map);
        ShowStatus("Изменения применены. Теперь матрицу можно сохранить в XML.", Brushes.ForestGreen);
    }

    private void ResetChangesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentMap is null)
            return;

        PopulateEditor(_currentMap);
        ShowStatus("Значения редактора восстановлены.", Brushes.DarkSlateGray);
    }

    private void SaveFileButton_Click(object sender, RoutedEventArgs e)
    {
        var map = ReadMapFromEditor();

        if (map is null)
            return;

        var dialog = new SaveFileDialog
        {
            Title = "Сохранить координаты",
            Filter = "XML-файлы (*.xml)|*.xml",
            DefaultExt = ".xml",
            AddExtension = true,
            FileName = "TransformationMap.xml",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var bytes = TransformationMapXmlSerializer.SerializeToUtf8(map);
            File.WriteAllBytes(dialog.FileName, bytes);

            TransformationMapXmlSerializer.Parse(File.ReadAllText(dialog.FileName));
            SetCurrentMap(map);
            ShowStatus($"Проверенный XML сохранён: {dialog.FileName}", Brushes.ForestGreen);
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
            ShowStatus("Матрица успешно загружена. Все 16 значений прошли проверку.", Brushes.ForestGreen);
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
        EditorGroupBox.IsEnabled = true;
    }

    private void PopulateEditor(TransformationMap map)
    {
        for (int row = 0; row < MatrixSize; row++)
        {
            for (int column = 0; column < MatrixSize; column++)
                _cellTextBoxes[row, column].Text = FormatValue(map[row, column]);
        }

        XTranslationTextBox.Text = FormatValue(map.TranslationX);
        YTranslationTextBox.Text = FormatValue(map.TranslationY);
        ZTranslationTextBox.Text = FormatValue(map.TranslationZ);
    }

    private TransformationMap? ReadMapFromEditor()
    {
        var values = new double[MatrixSize, MatrixSize];

        for (int row = 0; row < MatrixSize; row++)
        {
            for (int column = 0; column < MatrixSize; column++)
            {
                var textBox = _cellTextBoxes[row, column];

                if (!TryParseEditorValue(textBox.Text, out double value))
                {
                    string attributeName = $"m{row + 1}{column + 1}";
                    ShowError($"{attributeName} должен содержать конечное число без разделителей групп разрядов.");
                    textBox.Focus();
                    textBox.SelectAll();
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
        ShowStatus(message, Brushes.Firebrick);
        MessageBox.Show(this, message, "Проверка данных", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ShowStatus(string message, Brush foreground)
    {
        StatusTextBlock.Text = message;
        StatusTextBlock.Foreground = foreground;
    }
}
