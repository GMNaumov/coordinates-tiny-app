using System.Windows;
using System.Windows.Controls;
using CoordinatesTinyApp.ViewModels;

namespace CoordinatesTinyApp.Views;

public partial class MatrixEditorView : UserControl
{
    public MatrixEditorView()
    {
        InitializeComponent();
    }

    private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MatrixEditorViewModel oldViewModel)
            oldViewModel.FocusRequested -= FocusCell;

        if (e.NewValue is MatrixEditorViewModel viewModel)
            viewModel.FocusRequested += FocusCell;
    }

    private void FocusCell(int index)
    {
        string name = $"M{index / 4 + 1}{index % 4 + 1}TextBox";

        if (FindName(name) is TextBox textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }
}
