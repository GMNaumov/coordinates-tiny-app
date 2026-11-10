using System.Windows;
using System.Windows.Controls;
using CoordinatesTinyApp.ViewModels;

namespace CoordinatesTinyApp.Views;

public partial class MatrixComparisonView : UserControl
{
    public MatrixComparisonView()
    {
        InitializeComponent();
    }

    private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MatrixComparisonViewModel oldViewModel)
            oldViewModel.FocusRequested -= FocusInput;

        if (e.NewValue is MatrixComparisonViewModel viewModel)
            viewModel.FocusRequested += FocusInput;
    }

    private void FocusInput(bool isFirst)
    {
        var textBox = isFirst ? FirstInputTextBox : SecondInputTextBox;
        textBox.Focus();
    }
}
