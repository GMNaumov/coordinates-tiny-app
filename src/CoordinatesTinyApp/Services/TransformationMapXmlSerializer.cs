using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CoordinatesTinyApp.Models;

namespace CoordinatesTinyApp.Services;

internal static class TransformationMapXmlSerializer
{
    private const int MatrixSize = 4;
    private const long MaxDocumentLength = 64 * 1024;
    private const string ElementName = "TransformationMap";

    private static readonly string[] _attributeNames = CreateAttributeNames();

    public static TransformationMap Parse(string xml)
    {
        if (String.IsNullOrWhiteSpace(xml))
            throw new FormatException("Введите или загрузите один блок TransformationMap.");

        if (xml.Length > MaxDocumentLength)
            throw new FormatException("Размер блока координат превышает 64 КБ.");

        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxDocumentLength
            };

            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            var document = XDocument.Load(xmlReader, LoadOptions.None);
            var root = document.Root;

            if (root is null || root.Name != ElementName)
                throw new FormatException("Ожидался корневой элемент TransformationMap без пространства имён.");

            if (root.Elements().Any() || !String.IsNullOrWhiteSpace(root.Value))
                throw new FormatException("Элемент TransformationMap не должен содержать вложенные элементы или текст.");

            foreach (var attribute in root.Attributes())
            {
                if (!_attributeNames.Contains(attribute.Name.LocalName, StringComparer.Ordinal) || attribute.Name.NamespaceName.Length > 0)
                    throw new FormatException($"Неизвестный атрибут: {attribute.Name}.");
            }

            var values = new double[MatrixSize, MatrixSize];

            for (int row = 0; row < MatrixSize; row++)
            {
                for (int column = 0; column < MatrixSize; column++)
                {
                    string attributeName = GetAttributeName(row, column);
                    var attribute = root.Attribute(attributeName);

                    if (attribute is null)
                        throw new FormatException($"Отсутствует обязательный атрибут {attributeName}.");

                    if (!Double.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                        || !Double.IsFinite(value))
                    {
                        throw new FormatException($"Атрибут {attributeName} должен содержать конечное число с десятичной точкой.");
                    }

                    values[row, column] = value;
                }
            }

            return new TransformationMap(values);
        }
        catch (XmlException exception)
        {
            throw new FormatException($"Некорректный XML: {exception.Message}", exception);
        }
    }

    public static byte[] SerializeToUtf8(TransformationMap map)
    {
        if (map is null)
            throw new ArgumentNullException(nameof(map));

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace
        };

        using var stream = new MemoryStream();

        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartElement(ElementName);

            for (int row = 0; row < MatrixSize; row++)
            {
                for (int column = 0; column < MatrixSize; column++)
                {
                    writer.WriteAttributeString(
                        GetAttributeName(row, column),
                        map[row, column].ToString("R", CultureInfo.InvariantCulture));
                }
            }

            writer.WriteEndElement();
        }

        var result = stream.ToArray();
        Parse(Encoding.UTF8.GetString(result));
        return result;
    }

    private static string[] CreateAttributeNames()
    {
        var names = new string[MatrixSize * MatrixSize];

        for (int row = 0; row < MatrixSize; row++)
        {
            for (int column = 0; column < MatrixSize; column++)
                names[row * MatrixSize + column] = GetAttributeName(row, column);
        }

        return names;
    }

    private static string GetAttributeName(int row, int column)
    {
        return $"m{row + 1}{column + 1}";
    }
}
