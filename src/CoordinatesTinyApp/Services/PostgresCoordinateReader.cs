using CoordinatesTinyApp.Models;
using Npgsql;
using NpgsqlTypes;

namespace CoordinatesTinyApp.Services;

internal static class PostgresCoordinateReader
{
    public const int PageSize = 100;

    public static async Task TestConnectionAsync(DatabaseConnectionSettings settings, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(BuildConnectionString(settings));
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    public static async Task<IReadOnlyList<CoordinateRecord>> ReadPageAsync(
        DatabaseConnectionSettings settings, int offset, int? objectId = null, Guid? objectGuid = null,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        await using var connection = new NpgsqlConnection(BuildConnectionString(settings));
        await connection.OpenAsync(cancellationToken);

        // Ограничиваем и число строк, и размер text на сервере. Лишняя строка показывает наличие следующей страницы.
        const string sql = """
            SELECT "s_ObjectID", "s_Guid",
                   CASE WHEN octet_length(convert_to("Placement", 'UTF8')) <= 65536 THEN "Placement" ELSE NULL END,
                   COALESCE(octet_length(convert_to("Placement", 'UTF8')) > 65536, false)
            FROM dbo."Coordinates"
            WHERE (@objectId IS NULL OR "s_ObjectID" = @objectId)
              AND (@objectGuid IS NULL OR "s_Guid" = @objectGuid)
            ORDER BY "s_ObjectID" NULLS LAST, "s_Guid" NULLS LAST
            LIMIT @limit OFFSET @offset
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("objectId", NpgsqlDbType.Integer, (object?)objectId ?? DBNull.Value);
        command.Parameters.AddWithValue("objectGuid", NpgsqlDbType.Uuid, (object?)objectGuid ?? DBNull.Value);
        command.Parameters.AddWithValue("limit", PageSize + 1);
        command.Parameters.AddWithValue("offset", offset);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var records = new List<CoordinateRecord>();

        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new CoordinateRecord
            {
                ObjectId = reader.IsDBNull(0) ? null : reader.GetInt32(0),
                ObjectGuid = reader.IsDBNull(1) ? null : reader.GetGuid(1),
                Placement = reader.IsDBNull(2) ? null : reader.GetString(2),
                IsPlacementTooLarge = reader.GetBoolean(3)
            });
        }

        return records;
    }

    public static string BuildConnectionString(DatabaseConnectionSettings settings)
    {
        settings.Validate();
        return new NpgsqlConnectionStringBuilder
        {
            Host = settings.Server,
            Port = settings.Port,
            Database = settings.Database,
            Username = settings.Username,
            Password = settings.Password,
            Timeout = 10,
            CommandTimeout = 15,
            Pooling = false,
            PersistSecurityInfo = false,
            IncludeErrorDetail = false,
            ApplicationName = "CoordinatesTinyApp"
        }.ConnectionString;
    }
}
