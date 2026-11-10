using System.IO;
using System.Windows;
using CoordinatesTinyApp.Services;

namespace CoordinatesTinyApp.Views;

internal static class DatabaseDialogs
{
    public static DatabaseSettingsStore CreateSettingsStore()
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CoordinatesTinyApp", "database-settings.json");
        return new DatabaseSettingsStore(path, new WindowsPasswordProtector());
    }

    public static void ShowSettings(Window owner)
    {
        var dialog = new DatabaseSettingsWindow(CreateSettingsStore()) { Owner = owner };
        dialog.ShowDialog();
    }

    public static string? SelectPlacement(Window owner)
    {
        var dialog = new DatabaseRecordPickerWindow(CreateSettingsStore()) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.SelectedPlacement : null;
    }
}
