using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;
using Microsoft.Win32;

namespace CoordinatesTinyApp.Views;

public partial class MatrixComparisonView : UserControl
{
    public MatrixComparisonView()
    {
        InitializeComponent();
    }

    private void OpenFirstFileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFile(FirstInputTextBox, "Первая матрица");
    }

    private void OpenSecondFileButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFile(SecondInputTextBox, "Вторая матрица");
    }

    private void OpenFile(TextBox inputTextBox, string matrixName)
    {
        var dialog = new OpenFileDialog
        {
            Title = $"{matrixName}: открыть координаты",
            Filter = "XML-файлы (*.xml)|*.xml|Текстовые файлы (*.txt)|*.txt",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
            return;

        try
        {
            string xml = TransformationMapFileReader.ReadXml(dialog.FileName);
            TransformationMapXmlSerializer.Parse(xml);
            inputTextBox.Text = xml;
            ClearResult();
            ShowStatus($"{matrixName} загружена: {dialog.FileName}. Нажмите «Сравнить матрицы».", Brushes.DarkSlateGray);
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

    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized)
            return;

        // Результат относится только к тем исходным данным, по которым он был рассчитан.
        ClearResult();
        ShowStatus("Исходные данные изменены. Нажмите «Сравнить матрицы».", Brushes.DarkSlateGray);
    }

    private void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        ClearResult();
        var firstMap = ReadMap(FirstInputTextBox, "Первая матрица");

        if (firstMap is null)
            return;

        var secondMap = ReadMap(SecondInputTextBox, "Вторая матрица");

        if (secondMap is null)
            return;

        var differences = TransformationMapComparer.Compare(firstMap, secondMap);

        if (differences.Count == 0)
        {
            ShowStatus("Матрицы идентичны: все 16 числовых значений совпадают.", Brushes.ForestGreen);
        }
        else
        {
            DifferencesDataGrid.ItemsSource = differences;
            DifferencesDataGrid.Visibility = Visibility.Visible;
            ShowStatus($"Матрицы различаются. Количество отличающихся параметров: {differences.Count} из 16.", Brushes.DarkSlateGray);
        }
    }

    private TransformationMap? ReadMap(TextBox inputTextBox, string matrixName)
    {
        try
        {
            return TransformationMapXmlSerializer.Parse(inputTextBox.Text);
        }
        catch (FormatException exception)
        {
            ShowError($"{matrixName}: {exception.Message}");
            inputTextBox.Focus();
            return null;
        }
    }

    private void ClearResult()
    {
        DifferencesDataGrid.ItemsSource = null;
        DifferencesDataGrid.Visibility = Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
        ClearResult();
        ShowStatus(message, Brushes.Firebrick);
    }

    private void ShowStatus(string message, Brush foreground)
    {
        ComparisonStatusTextBlock.Text = message;
        ComparisonStatusTextBlock.Foreground = foreground;
    }
}
