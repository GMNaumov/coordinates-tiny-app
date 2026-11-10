using System.IO;
using System.Text.Json;
using CoordinatesTinyApp.Models;

namespace CoordinatesTinyApp.Services;

internal sealed class DatabaseSettingsStore
{
    private const int MaxFileSize = 64 * 1024;
    private readonly IPasswordProtector _passwordProtector;

    public DatabaseSettingsStore(string filePath, IPasswordProtector passwordProtector)
    {
        FilePath = Path.GetFullPath(filePath);
        _passwordProtector = passwordProtector;
    }

    public string FilePath { get; }

    public DatabaseConnectionSettings? Load()
    {
        // File.Exists скрывает ошибки доступа, поэтому отсутствие файла обрабатывается отдельно.
        byte[] bytes;

        try
        {
            using var stream = File.OpenRead(FilePath);

            if (stream.Length > MaxFileSize)
                throw new FormatException("Файл настроек подключения превышает 64 КБ.");

            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int count;

            while ((count = stream.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (buffer.Length + count > MaxFileSize)
                    throw new FormatException("Файл настроек подключения превышает 64 КБ.");

                buffer.Write(chunk, 0, count);
            }

            bytes = buffer.ToArray();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        var stored = JsonSerializer.Deserialize<StoredSettings>(bytes)
            ?? throw new FormatException("Файл настроек подключения пуст.");

        if (stored.Version != 1 || stored.Server is null || stored.Database is null
            || stored.Username is null || String.IsNullOrEmpty(stored.EncryptedPassword))
        {
            throw new FormatException("Файл настроек подключения повреждён или имеет неподдерживаемый формат.");
        }

        var settings = new DatabaseConnectionSettings
        {
            Server = stored.Server,
            Port = stored.Port,
            Database = stored.Database,
            Username = stored.Username,
            Password = _passwordProtector.Unprotect(stored.EncryptedPassword)
        };
        settings.Validate();
        return settings;
    }

    public void Save(DatabaseConnectionSettings settings)
    {
        settings.Validate();
        var stored = new StoredSettings
        {
            Version = 1,
            Server = settings.Server,
            Port = settings.Port,
            Database = settings.Database,
            Username = settings.Username,
            EncryptedPassword = _passwordProtector.Protect(settings.Password)
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(stored, new JsonSerializerOptions { WriteIndented = true });

        if (bytes.Length > MaxFileSize)
            throw new FormatException("Настройки подключения превышают 64 КБ.");

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporaryPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            // Новый файл заменяет старый только после полной записи зашифрованных настроек.
            File.WriteAllBytes(temporaryPath, bytes);
            File.Move(temporaryPath, FilePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private sealed class StoredSettings
    {
        public int Version { get; init; }

        public string? Server { get; init; }

        public int Port { get; init; }

        public string? Database { get; init; }

        public string? Username { get; init; }

        public string? EncryptedPassword { get; init; }
    }
}
