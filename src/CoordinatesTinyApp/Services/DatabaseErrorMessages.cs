using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;

namespace CoordinatesTinyApp.Services;

internal static class DatabaseErrorMessages
{
    public static bool IsExpected(Exception exception)
    {
        return exception is NpgsqlException or IOException or UnauthorizedAccessException
            or CryptographicException or JsonException or FormatException or ArgumentException or TimeoutException or InvalidCastException;
    }

    public static string GetMessage(Exception exception)
    {
        // Серверные сообщения и строки подключения могут содержать чувствительные данные.
        return exception switch
        {
            PostgresException { SqlState: "28P01" or "28000" } => "Не удалось войти в PostgreSQL. Проверьте логин, пароль и правила доступа сервера.",
            PostgresException { SqlState: "3D000" } => "Указанная база данных не существует.",
            PostgresException { SqlState: "42P01" or "3F000" } => "Не найдена таблица dbo.\"Coordinates\". Проверьте выбранную базу данных и схему.",
            PostgresException { SqlState: "42703" } => "В таблице отсутствует одно из полей: s_ObjectID, s_Guid, Placement.",
            PostgresException { SqlState: "42501" } => "Недостаточно прав для чтения dbo.\"Coordinates\".",
            PostgresException => "PostgreSQL не смог выполнить запрос. Проверьте доступность базы данных и структуру таблицы.",
            NpgsqlException or TimeoutException => "Не удалось выполнить подключение или запрос к PostgreSQL. Проверьте сервер, порт и доступность базы данных.",
            CryptographicException => "Не удалось расшифровать пароль. Введите настройки заново под текущей учётной записью Windows.",
            JsonException => "Файл настроек подключения содержит некорректный JSON. Введите настройки заново.",
            UnauthorizedAccessException => "Нет доступа к файлу настроек подключения.",
            IOException => "Не удалось прочитать или сохранить файл настроек подключения.",
            InvalidCastException => "Типы полей в dbo.\"Coordinates\" должны быть integer, uuid и text.",
            FormatException => "Проверьте данные: неверный формат настроек или матрицы.",
            _ => "Проверьте параметры подключения к PostgreSQL."
        };
    }
}
