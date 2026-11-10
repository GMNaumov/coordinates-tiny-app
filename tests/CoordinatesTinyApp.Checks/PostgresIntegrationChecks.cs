using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;
using Npgsql;

namespace CoordinatesTinyApp.Checks;

internal static class PostgresIntegrationChecks
{
    public static async Task RunAsync()
    {
        // Режим явно создаёт данные только в пустой одноразовой БД, запускаемой тестовым скриптом.
        var settings = new DatabaseConnectionSettings
        {
            Server = Environment.GetEnvironmentVariable("COORDINATES_TEST_PGHOST") ?? "localhost",
            Port = Int32.Parse(Environment.GetEnvironmentVariable("COORDINATES_TEST_PGPORT") ?? "5432"),
            Database = "coordinates_checks",
            Username = "postgres",
            Password = Environment.GetEnvironmentVariable("COORDINATES_TEST_PGPASSWORD")
                ?? throw new InvalidOperationException("Set COORDINATES_TEST_PGPASSWORD for a disposable PostgreSQL instance.")
        };
        await PostgresCoordinateReader.TestConnectionAsync(settings);
        Console.WriteLine("PASS PostgreSQL connection without Coordinates table");
        await ExpectPostgresErrorAsync("42P01", () => PostgresCoordinateReader.ReadPageAsync(settings, 0));

        await using var connection = new NpgsqlConnection(PostgresCoordinateReader.BuildConnectionString(settings));
        await connection.OpenAsync();
        const string sql = """
            CREATE SCHEMA dbo;
            CREATE TABLE dbo."Coordinates" ("s_ObjectID" integer, "s_Guid" uuid, "Placement" text);
            INSERT INTO dbo."Coordinates"
                SELECT n, md5(n::text)::uuid, @xml FROM generate_series(1, 205) AS n;
            INSERT INTO dbo."Coordinates" VALUES
                (206, md5('206')::uuid, NULL), (207, md5('207')::uuid, ''),
                (208, md5('208')::uuid, '<bad/>'), (209, md5('209')::uuid, repeat(' ', 65537)),
                (210, md5('210')::uuid, @xml || repeat(' ', 65536 - octet_length(@xml))),
                (211, md5('211')::uuid, repeat('я', 32769)), (NULL, NULL, @xml);
            CREATE ROLE coordinates_readonly LOGIN PASSWORD 'fixture-reader-password';
            GRANT CONNECT ON DATABASE coordinates_checks TO coordinates_readonly;
            GRANT USAGE ON SCHEMA dbo TO coordinates_readonly;
            GRANT SELECT ON dbo."Coordinates" TO coordinates_readonly;
            """;
        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("xml", Program.CreateXml("0.17200000000000001"));
            await command.ExecuteNonQueryAsync();
        }

        var readerSettings = new DatabaseConnectionSettings
        {
            Server = settings.Server, Port = settings.Port, Database = settings.Database,
            Username = "coordinates_readonly", Password = "fixture-reader-password"
        };
        await PostgresCoordinateReader.TestConnectionAsync(readerSettings);
        var first = await PostgresCoordinateReader.ReadPageAsync(readerSettings, 0);
        var second = await PostgresCoordinateReader.ReadPageAsync(readerSettings, 100);
        var third = await PostgresCoordinateReader.ReadPageAsync(readerSettings, 200);
        Program.Require(first.Count == 101 && first[0].ObjectId == 1 && first[100].ObjectId == 101,
            "First page must include one lookahead row.");
        Program.Require(second.Count == 101 && second[0].ObjectId == 101 && second[100].ObjectId == 201,
            "Second page must follow the first without skipping rows.");
        Program.Require(third.Count == 12 && third[^1].ObjectId is null && third[^1].ObjectGuid is null,
            "Last page must preserve nullable identifiers.");
        Program.Require(first[0].GetValidatedPlacement() == Program.CreateXml("0.17200000000000001"),
            "Database XML must retain its precision and formatting.");
        var byId = await PostgresCoordinateReader.ReadPageAsync(readerSettings, 0, objectId: 42);
        var byGuid = await PostgresCoordinateReader.ReadPageAsync(readerSettings, 0, objectGuid: first[0].ObjectGuid);
        Program.Require(byId.Count == 1 && byId[0].ObjectId == 42, "ID filter must select exactly one record.");
        Program.Require(byGuid.Count == 1 && byGuid[0].ObjectId == 1, "UUID filter must select exactly one record.");
        Program.Require((await PostgresCoordinateReader.ReadPageAsync(readerSettings, 0, objectId: -1)).Count == 0,
            "Unmatched search must return an empty list.");

        foreach (int id in new[] { 206, 207, 208, 209, 211 })
        {
            var records = await PostgresCoordinateReader.ReadPageAsync(readerSettings, 0, objectId: id);
            Program.ExpectException<FormatException>(() => records[0].GetValidatedPlacement());

            if (id is 209 or 211)
                Program.Require(records[0].IsPlacementTooLarge && records[0].Placement is null, "Oversized UTF-8 text must not reach the client.");
        }

        var boundary = await PostgresCoordinateReader.ReadPageAsync(readerSettings, 0, objectId: 210);
        Program.Require(boundary[0].GetValidatedPlacement().Length == 65536, "64 KB boundary must remain loadable.");
        var wrongPassword = new DatabaseConnectionSettings
        {
            Server = settings.Server, Port = settings.Port, Database = settings.Database,
            Username = readerSettings.Username, Password = "incorrect-fixture-password"
        };
        await ExpectPostgresErrorAsync("28P01", () => PostgresCoordinateReader.TestConnectionAsync(wrongPassword));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await PostgresCoordinateReader.TestConnectionAsync(readerSettings, cancellation.Token);
            throw new InvalidOperationException("Cancelled connection must not succeed.");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("PASS cancelled PostgreSQL connection");
        }

        Console.WriteLine("PASS PostgreSQL pagination, filters, read-only login, invalid XML, NULL, size limits and wrong password");
    }

    private static async Task ExpectPostgresErrorAsync(string sqlState, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (PostgresException exception) when (exception.SqlState == sqlState)
        {
            return;
        }

        throw new InvalidOperationException($"Expected PostgreSQL error {sqlState}.");
    }
}
