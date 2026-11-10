using CoordinatesTinyApp.Models;

namespace CoordinatesTinyApp.Services;

internal sealed class PostgresCoordinateDatabase : ICoordinateDatabase
{
    public Task TestConnectionAsync(DatabaseConnectionSettings settings, CancellationToken cancellationToken)
    {
        return PostgresCoordinateReader.TestConnectionAsync(settings, cancellationToken);
    }

    public Task<IReadOnlyList<CoordinateRecord>> ReadPageAsync(DatabaseConnectionSettings settings, int offset,
        int? objectId, Guid? objectGuid, CancellationToken cancellationToken)
    {
        return PostgresCoordinateReader.ReadPageAsync(settings, offset, objectId, objectGuid, cancellationToken);
    }
}
