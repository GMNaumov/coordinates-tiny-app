using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CoordinatesTinyApp.Services;
using CoordinatesTinyApp.ViewModels;

namespace CoordinatesTinyApp.Views;

public partial class DatabaseRecordPickerWindow : Window
{
    private readonly DatabaseRecordPickerViewModel _viewModel;

    internal DatabaseRecordPickerWindow(IDatabaseSettingsStore store, ICoordinateDatabase? database = null)
    {
        InitializeComponent();
        _viewModel = new DatabaseRecordPickerViewModel(store, database ?? new PostgresCoordinateDatabase(), new WindowDialogService(this, store));
        _viewModel.CloseRequested += CompleteDialog;
        DataContext = _viewModel;
    }

    public string? SelectedPlacement => _viewModel.SelectedPlacement;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void RecordsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Двойной щелчок по заголовку или пустому месту не подтверждает прежнее выделение.
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(RecordsDataGrid, source) is DataGridRow)
            _viewModel.SelectCommand.Execute(null);
    }

    private void CompleteDialog()
    {
        DialogResult = true;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _viewModel.CloseRequested -= CompleteDialog;
        _viewModel.Close();
    }
}
