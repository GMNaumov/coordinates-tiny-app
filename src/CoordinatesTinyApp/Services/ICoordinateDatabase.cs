using CoordinatesTinyApp.Models;

namespace CoordinatesTinyApp.Services;

internal interface ICoordinateDatabase
{
    Task TestConnectionAsync(DatabaseConnectionSettings settings, CancellationToken cancellationToken);

    Task<IReadOnlyList<CoordinateRecord>> ReadPageAsync(DatabaseConnectionSettings settings, int offset,
        int? objectId, Guid? objectGuid, CancellationToken cancellationToken);
}
