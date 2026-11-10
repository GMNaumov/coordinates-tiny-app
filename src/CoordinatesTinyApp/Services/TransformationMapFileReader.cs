using System.IO;

namespace CoordinatesTinyApp.Services;

internal static class TransformationMapFileReader
{
    private const long MaxInputFileLength = 64 * 1024;

    public static string ReadXml(string fileName)
    {
        string extension = Path.GetExtension(fileName);

        if (!extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("Поддерживаются только файлы .txt и .xml.");
        }

        var fileInfo = new FileInfo(fileName);

        if (fileInfo.Length > MaxInputFileLength)
            throw new FormatException("Размер файла превышает 64 КБ.");

        return File.ReadAllText(fileName);
    }
}
