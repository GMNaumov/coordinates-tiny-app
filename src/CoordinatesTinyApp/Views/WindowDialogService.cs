using System.Windows;
using CoordinatesTinyApp.Services;
using Microsoft.Win32;

namespace CoordinatesTinyApp.Views;

internal sealed class WindowDialogService : IUserDialogService
{
    private readonly Window _owner;
    private readonly IDatabaseSettingsStore _store;

    public WindowDialogService(Window owner, IDatabaseSettingsStore? store = null)
    {
        _owner = owner;
        _store = store ?? DatabaseSettingsStore.CreateDefault();
    }

    public string? SelectOpenFile(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "XML-файлы (*.xml)|*.xml|Текстовые файлы (*.txt)|*.txt",
            CheckFileExists = true,
            Multiselect = false
        };
        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    public string? SelectSaveFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Сохранить координаты",
            Filter = "XML-файлы (*.xml)|*.xml",
            DefaultExt = ".xml",
            AddExtension = true,
            FileName = "TransformationMap.xml",
            OverwritePrompt = true
        };
        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    public string? SelectPlacement()
    {
        var dialog = new DatabaseRecordPickerWindow(_store) { Owner = _owner };
        return dialog.ShowDialog() == true ? dialog.SelectedPlacement : null;
    }

    public bool ShowDatabaseSettings()
    {
        var dialog = new DatabaseSettingsWindow(_store) { Owner = _owner };
        return dialog.ShowDialog() == true;
    }

    public void ShowWarning(string message)
    {
        MessageBox.Show(_owner, message, "Проверка данных", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
