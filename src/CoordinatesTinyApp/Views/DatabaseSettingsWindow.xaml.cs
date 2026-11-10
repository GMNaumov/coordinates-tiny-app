using System.Windows;
using CoordinatesTinyApp.Services;
using CoordinatesTinyApp.ViewModels;

namespace CoordinatesTinyApp.Views;

public partial class DatabaseSettingsWindow : Window
{
    private readonly DatabaseSettingsViewModel _viewModel;

    internal DatabaseSettingsWindow(IDatabaseSettingsStore store)
    {
        InitializeComponent();
        _viewModel = new DatabaseSettingsViewModel(store, new PostgresCoordinateDatabase());
        _viewModel.CloseRequested += CompleteDialog;
        DataContext = _viewModel;
        DatabasePasswordBox.Password = _viewModel.Password;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        // PasswordBox не поддерживает Binding. Пароль не помещается в dependency property.
        if (DataContext is DatabaseSettingsViewModel viewModel)
            viewModel.Password = DatabasePasswordBox.Password;
    }

    private void CompleteDialog()
    {
        DialogResult = true;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _viewModel.CloseRequested -= CompleteDialog;
        _viewModel.Close();
        DatabasePasswordBox.Clear();
    }
}
