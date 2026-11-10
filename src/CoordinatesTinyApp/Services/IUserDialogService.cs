namespace CoordinatesTinyApp.Services;

internal interface IUserDialogService
{
    string? SelectOpenFile(string title);

    string? SelectSaveFile();

    string? SelectPlacement();

    bool ShowDatabaseSettings();

    void ShowWarning(string message);
}
