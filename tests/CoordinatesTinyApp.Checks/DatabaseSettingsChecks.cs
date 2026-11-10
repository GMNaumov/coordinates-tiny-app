using System.Security.Cryptography;
using System.Text.Json;
using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;
using Npgsql;

namespace CoordinatesTinyApp.Checks;

internal static class DatabaseSettingsChecks
{
    public static void SettingsAreStoredWithoutPlaintextPassword()
    {
        WithTemporaryPath(path =>
        {
            // Подмена проверяет контракт хранилища; настоящий DPAPI проверяется отдельно на Windows.
            var store = new DatabaseSettingsStore(path, new MemoryPasswordProtector());
            Program.Require(store.Load() is null, "Missing settings must not be an error.");
            var settings = CreateSettings();
            store.Save(settings);
            string json = File.ReadAllText(path);
            using var document = JsonDocument.Parse(json);
            Program.Require(!document.RootElement.TryGetProperty("Password", out _), "Plaintext password property must not exist.");
            Program.Require(!json.Contains(settings.Password, StringComparison.Ordinal), "Password must not be written verbatim.");
            var restored = store.Load()!;
            Program.Require(restored.Server == settings.Server && restored.Port == settings.Port
                && restored.Database == settings.Database && restored.Username == settings.Username
                && restored.Password == settings.Password, "All connection fields must round-trip.");
            store.Save(CreateSettings("changed"));
            Program.Require(store.Load()!.Password == "changed", "Existing settings must be replaceable.");
            Program.Require(Directory.GetFiles(Path.GetDirectoryName(path)!).Length == 1, "Temporary settings files must be removed.");
        });
    }

    public static void FailedSavePreservesPreviousSettings()
    {
        WithTemporaryPath(path =>
        {
            var protector = new MemoryPasswordProtector();
            var store = new DatabaseSettingsStore(path, protector);
            store.Save(CreateSettings());
            string original = File.ReadAllText(path);
            protector.ThrowOnProtect = true;
            Program.ExpectException<CryptographicException>(() => store.Save(CreateSettings("replacement")));
            Program.Require(File.ReadAllText(path) == original, "Encryption failure must preserve existing settings.");
            Program.ExpectException<FormatException>(() => store.Save(new DatabaseConnectionSettings()));
            Program.Require(File.ReadAllText(path) == original, "Validation failure must preserve existing settings.");
        });
    }

    public static void CorruptSettingsAreRejected()
    {
        WithTemporaryPath(path =>
        {
            var store = new DatabaseSettingsStore(path, new MemoryPasswordProtector());
            File.WriteAllText(path, "{");
            Program.ExpectException<JsonException>(() => store.Load());

            foreach (string json in new[] { "null", "{}", "{\"Version\":2}", "{\"Version\":1,\"Password\":\"plaintext\"}" })
            {
                File.WriteAllText(path, json);
                Program.ExpectException<FormatException>(() => store.Load());
            }

            File.WriteAllText(path, new String(' ', 65537));
            Program.ExpectException<FormatException>(() => store.Load());
            store.Save(CreateSettings());
            using var original = JsonDocument.Parse(File.ReadAllText(path));
            string ciphertext = original.RootElement.GetProperty("EncryptedPassword").GetString()!;
            File.WriteAllText(path, File.ReadAllText(path).Replace(ciphertext, "unreadable-ciphertext", StringComparison.Ordinal));
            Program.ExpectException<CryptographicException>(() => store.Load());
        });
    }

    public static void ConnectionParametersAreValidatedAndEscaped()
    {
        var settings = CreateSettings();
        var builder = new NpgsqlConnectionStringBuilder(PostgresCoordinateReader.BuildConnectionString(settings));
        Program.Require(builder.Host == settings.Server && builder.Database == settings.Database
            && builder.Username == settings.Username && builder.Password == settings.Password
            && builder.Port == settings.Port, "Semicolons and quotes must remain part of parameter values.");
        Program.Require(!builder.Pooling && !builder.PersistSecurityInfo && !builder.IncludeErrorDetail,
            "Credentials must not be kept in a shared connection pool or disclosed by diagnostics.");

        foreach (int port in new[] { -1, 0, 65536 })
            Program.ExpectException<FormatException>(() => new DatabaseConnectionSettings { Database = "db", Username = "user", Port = port }.Validate());

        Program.ExpectException<FormatException>(() => new DatabaseConnectionSettings { Server = " ", Database = "db", Username = "u" }.Validate());
        Program.ExpectException<FormatException>(() => new DatabaseConnectionSettings { Database = " ", Username = "u" }.Validate());
        Program.ExpectException<FormatException>(() => new DatabaseConnectionSettings { Database = "db", Username = " " }.Validate());
        Program.ExpectException<ArgumentOutOfRangeException>(() => PostgresCoordinateReader.ReadPageAsync(settings, -1).GetAwaiter().GetResult());
    }

    public static void SelectedPlacementMustBeValid()
    {
        string xml = Program.CreateXml("0.17200000000000001");
        Program.Require(new CoordinateRecord { Placement = xml }.GetValidatedPlacement() == xml,
            "Selecting a record must preserve its original XML and precision.");

        foreach (string? input in new[] { null, "", "<TransformationMap/>", xml + xml, "<!DOCTYPE TransformationMap>" + xml })
            Program.ExpectException<FormatException>(() => new CoordinateRecord { Placement = input }.GetValidatedPlacement());

        Program.ExpectException<FormatException>(() => new CoordinateRecord { IsPlacementTooLarge = true }.GetValidatedPlacement());
    }

    public static void ErrorsDoNotExposeConnectionSecrets()
    {
        const string secret = "sensitive-password";
        Exception[] errors =
        {
            new NpgsqlException(secret), new PostgresException(secret, "ERROR", "ERROR", "28P01"),
            new IOException(secret), new JsonException(secret), new FormatException(secret), new ArgumentException(secret)
        };

        foreach (var error in errors)
            Program.Require(!DatabaseErrorMessages.GetMessage(error).Contains(secret, StringComparison.Ordinal), "Errors must not expose secrets.");
    }

    public static void WindowsPasswordEncryptionRoundTrips()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI requires Windows.");

        var protector = new WindowsPasswordProtector();

        foreach (string password in new[] { "", "пароль;\" with spaces 🔐" })
        {
            string encrypted = protector.Protect(password);
            Program.Require(protector.Unprotect(encrypted) == password, "DPAPI must preserve the password.");
            Program.Require(encrypted != protector.Protect(password), "DPAPI ciphertext must be randomized.");
            var bytes = Convert.FromBase64String(encrypted);
            bytes[^1] ^= 1;
            bool rejected = false;

            try
            {
                protector.Unprotect(Convert.ToBase64String(bytes));
            }
            catch (CryptographicException)
            {
                rejected = true;
            }

            Program.Require(rejected, "DPAPI must reject tampered ciphertext.");
        }

        WithTemporaryPath(path =>
        {
            var store = new DatabaseSettingsStore(path, protector);
            store.Save(CreateSettings());
            Program.Require(store.Load()!.Password == CreateSettings().Password, "Settings file must round-trip with real DPAPI.");
        });
    }

    private static DatabaseConnectionSettings CreateSettings(string password = "пароль;\" secret 🔐")
    {
        return new DatabaseConnectionSettings
        {
            Server = "server.example", Port = 5433, Database = "db;Pooling=true",
            Username = "user;Timeout=99", Password = password
        };
    }

    private static void WithTemporaryPath(Action<string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "coordinates-db-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.json");

        try
        {
            check(path);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class MemoryPasswordProtector : IPasswordProtector
    {
        private readonly Dictionary<string, string> _passwords = new();

        public bool ThrowOnProtect { get; set; }

        public string Protect(string password)
        {
            if (ThrowOnProtect)
                throw new CryptographicException("Simulated encryption failure.");

            string token = Guid.NewGuid().ToString("N");
            _passwords.Add(token, password);
            return token;
        }

        public string Unprotect(string encryptedPassword)
        {
            return _passwords.TryGetValue(encryptedPassword, out string? password)
                ? password : throw new CryptographicException("Simulated decryption failure.");
        }
    }
}
