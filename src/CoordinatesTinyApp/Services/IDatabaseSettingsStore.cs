using CoordinatesTinyApp.Models;

namespace CoordinatesTinyApp.Services;

internal interface IDatabaseSettingsStore
{
    string FilePath { get; }

    DatabaseConnectionSettings? Load();

    void Save(DatabaseConnectionSettings settings);
}
