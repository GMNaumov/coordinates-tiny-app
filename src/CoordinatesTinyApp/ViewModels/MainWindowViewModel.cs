using CoordinatesTinyApp.Infrastructure;
using CoordinatesTinyApp.Services;

namespace CoordinatesTinyApp.ViewModels;

internal sealed class MainWindowViewModel
{
    public MainWindowViewModel(IUserDialogService dialogs)
    {
        Editor = new MatrixEditorViewModel(dialogs);
        Comparison = new MatrixComparisonViewModel(dialogs);
        DatabaseSettingsCommand = new RelayCommand(() => dialogs.ShowDatabaseSettings());
    }

    public MatrixEditorViewModel Editor { get; }

    public MatrixComparisonViewModel Comparison { get; }

    public RelayCommand DatabaseSettingsCommand { get; }
}
