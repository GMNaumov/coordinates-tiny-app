using System.Text.Json.Serialization;

namespace CoordinatesTinyApp.Models;

internal sealed class DatabaseConnectionSettings
{
    public string Server { get; init; } = "localhost";

    public int Port { get; init; } = 5432;

    public string Database { get; init; } = "";

    public string Username { get; init; } = "";

    [JsonIgnore]
    public string Password { get; init; } = "";

    public void Validate()
    {
        if (String.IsNullOrWhiteSpace(Server))
            throw new FormatException("Укажите сервер PostgreSQL.");

        if (Port < 1 || Port > 65535)
            throw new FormatException("Порт должен быть целым числом от 1 до 65535.");

        if (String.IsNullOrWhiteSpace(Database))
            throw new FormatException("Укажите имя базы данных.");

        if (String.IsNullOrWhiteSpace(Username))
            throw new FormatException("Укажите логин PostgreSQL.");
    }
}
