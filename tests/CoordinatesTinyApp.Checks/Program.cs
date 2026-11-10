using System.Globalization;
using System.Text;
using CoordinatesTinyApp.Models;
using CoordinatesTinyApp.Services;

namespace CoordinatesTinyApp.Checks;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Action[] checks =
        {
            IdenticalMatricesHaveNoDifferences,
            XmlFormattingDoesNotAffectEquality,
            SignedZerosAreEqual,
            EveryParameterIsCompared,
            DifferencesContainValuesAndSignedDelta,
            SmallDifferencesAreDetected,
            OverflowDoesNotHideDifferences,
            ComparisonDoesNotChangeInputs,
            InvalidXmlIsRejected,
            FileRestrictionsArePreserved,
            SerializationPreservesValues,
            NullMatricesAreRejected,
            DatabaseSettingsChecks.SettingsAreStoredWithoutPlaintextPassword,
            DatabaseSettingsChecks.FailedSavePreservesPreviousSettings,
            DatabaseSettingsChecks.CorruptSettingsAreRejected,
            DatabaseSettingsChecks.ConnectionParametersAreValidatedAndEscaped,
            DatabaseSettingsChecks.SelectedPlacementMustBeValid,
            DatabaseSettingsChecks.ErrorsDoNotExposeConnectionSecrets
        };

        if (OperatingSystem.IsWindows())
        {
            checks = checks.Append(DatabaseSettingsChecks.WindowsPasswordEncryptionRoundTrips).ToArray();
        }
        else
        {
            Console.WriteLine("SKIP WindowsPasswordEncryptionRoundTrips: DPAPI requires Windows.");
        }

        foreach (var check in checks)
        {
            try
            {
                check();
                Console.WriteLine($"PASS {check.Method.Name}");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"FAIL {check.Method.Name}: {exception}");
                return 1;
            }
        }

        Console.WriteLine($"All {checks.Length} checks passed.");

        if (args.Contains("--postgres-fixture", StringComparer.Ordinal))
        {
            try
            {
                await PostgresIntegrationChecks.RunAsync();
                Console.WriteLine("PASS PostgreSQL integration checks (disposable fixture)");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"FAIL PostgreSQL integration checks: {exception}");
                return 1;
            }
        }

        return 0;
    }

    private static void IdenticalMatricesHaveNoDifferences()
    {
        var firstMap = TransformationMapXmlSerializer.Parse(CreateXml("0.17200000000000001"));
        var secondMap = TransformationMapXmlSerializer.Parse(CreateXml("0.17200000000000001"));
        Require(TransformationMapComparer.Compare(firstMap, secondMap).Count == 0, "Equal matrices must match.");
        Require(TransformationMapComparer.Compare(firstMap, firstMap).Count == 0, "A matrix must match itself.");
    }

    private static void XmlFormattingDoesNotAffectEquality()
    {
        string firstXml = CreateXml("1");
        string secondXml = CreateXml("1.000").Replace("<TransformationMap ", "<TransformationMap\n ")
            .Replace("m11=\"1.000\" ", "")
            .Replace("/>", "m11=\"1e0\" />");
        var firstMap = TransformationMapXmlSerializer.Parse(firstXml);
        var secondMap = TransformationMapXmlSerializer.Parse(secondXml);
        Require(TransformationMapComparer.Compare(firstMap, secondMap).Count == 0, "Only numeric values matter.");
    }

    private static void SignedZerosAreEqual()
    {
        var firstMap = TransformationMapXmlSerializer.Parse(CreateXml("-0"));
        var secondMap = TransformationMapXmlSerializer.Parse(CreateXml("0"));
        Require(TransformationMapComparer.Compare(firstMap, secondMap).Count == 0, "Signed zeros must match.");
    }

    private static void EveryParameterIsCompared()
    {
        var firstMap = TransformationMapXmlSerializer.Parse(CreateXml("0"));
        var secondMap = TransformationMapXmlSerializer.Parse(CreateXml("1"));
        var differences = TransformationMapComparer.Compare(firstMap, secondMap);
        Require(differences.Count == 16, "All 16 differences must be returned.");

        for (int index = 0; index < differences.Count; index++)
        {
            string expectedName = $"m{index / 4 + 1}{index % 4 + 1}";
            Require(differences[index].ParameterName == expectedName, $"Missing or misplaced {expectedName}.");
        }
    }

    private static void DifferencesContainValuesAndSignedDelta()
    {
        string xml = CreateXml("0");
        var firstMap = TransformationMapXmlSerializer.Parse(xml.Replace("m41=\"0\"", "m41=\"-0.5\""));
        var secondMap = TransformationMapXmlSerializer.Parse(xml.Replace("m41=\"0\"", "m41=\"0.25\""));
        var differences = TransformationMapComparer.Compare(firstMap, secondMap);
        Require(differences.Count == 1, "Only m41 differs.");
        var difference = differences[0];
        Require(difference.ParameterName == "m41", "Translation X must be identified as m41.");
        Require(difference.FirstValue == -0.5 && difference.SecondValue == 0.25, "Original values must be preserved.");
        Require(difference.Difference == 0.75, "Delta must be second minus first.");
        Require(TransformationMapComparer.Compare(secondMap, firstMap)[0].Difference == -0.75, "Reverse delta must be negative.");
    }

    private static void SmallDifferencesAreDetected()
    {
        double nextValue = Double.BitIncrement(1);
        var firstMap = TransformationMapXmlSerializer.Parse(CreateXml("1"));
        var secondMap = TransformationMapXmlSerializer.Parse(CreateXml("1")
            .Replace("m44=\"1\"", $"m44=\"{nextValue.ToString("R", CultureInfo.InvariantCulture)}\""));
        var differences = TransformationMapComparer.Compare(firstMap, secondMap);
        Require(differences.Count == 1 && differences[0].ParameterName == "m44", "Even a one-bit difference must be detected.");
        Require(differences[0].Difference > 0, "Tiny delta must not be rounded to zero.");
    }

    private static void OverflowDoesNotHideDifferences()
    {
        string maximum = Double.MaxValue.ToString("R", CultureInfo.InvariantCulture);
        var firstMap = TransformationMapXmlSerializer.Parse(CreateXml("-" + maximum));
        var secondMap = TransformationMapXmlSerializer.Parse(CreateXml(maximum));
        var differences = TransformationMapComparer.Compare(firstMap, secondMap);
        Require(differences.Count == 16, "Overflow must not hide differences.");
        Require(differences.All(difference => difference.Difference is null), "Overflow must be explicitly represented.");
    }

    private static void ComparisonDoesNotChangeInputs()
    {
        var firstMap = TransformationMapXmlSerializer.Parse(CreateXml("0"));
        var secondMap = TransformationMapXmlSerializer.Parse(CreateXml("1"));
        var differences = TransformationMapComparer.Compare(firstMap, secondMap);
        Require(firstMap[3, 2] == 0 && secondMap[3, 2] == 1, "Comparison must preserve input matrices.");
        TransformationMapComparer.Compare(secondMap, secondMap);
        Require(differences.Count == 16 && differences[15].SecondValue == 1, "Later comparisons must preserve previous results.");
    }

    private static void InvalidXmlIsRejected()
    {
        string xml = CreateXml("1");
        string[] invalidInputs =
        {
            "", xml + xml, xml.Replace("m11=\"1\"", ""), xml.Replace("m11=\"1\"", "extra=\"1\""),
            xml.Replace("m11=\"1\"", "m11=\"NaN\""), xml.Replace("m11=\"1\"", "m11=\"Infinity\""),
            xml.Replace("m11=\"1\"", "m11=\"1,25\""), xml.Replace("/>", "><Child /></TransformationMap>"),
            xml.Replace("/>", ">text</TransformationMap>"), xml.Replace("<TransformationMap ", "<TransformationMap xmlns=\"urn:test\" "),
            "<!DOCTYPE TransformationMap [<!ENTITY value '1'>]>" + xml,
            new String(' ', 64 * 1024) + xml
        };

        foreach (string invalidInput in invalidInputs)
            ExpectException<FormatException>(() => TransformationMapXmlSerializer.Parse(invalidInput));
    }

    private static void FileRestrictionsArePreserved()
    {
        string directory = Path.Combine(Path.GetTempPath(), "coordinates-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string fileName = Path.Combine(directory, "matrix.XML");
        string textFileName = Path.Combine(directory, "matrix.txt");

        try
        {
            string xml = CreateXml("1");
            File.WriteAllText(fileName, xml, new UTF8Encoding(false));
            Require(TransformationMapFileReader.ReadXml(fileName) == xml, "XML file must load.");
            File.WriteAllText(textFileName, xml, new UTF8Encoding(false));
            Require(TransformationMapFileReader.ReadXml(textFileName) == xml, "TXT file must load.");
            ExpectException<FormatException>(() => TransformationMapFileReader.ReadXml(Path.Combine(directory, "matrix.csv")));
            ExpectException<FileNotFoundException>(() => TransformationMapFileReader.ReadXml(Path.Combine(directory, "missing.xml")));
            File.WriteAllText(fileName, new String(' ', 64 * 1024), new UTF8Encoding(false));
            Require(TransformationMapFileReader.ReadXml(fileName).Length == 64 * 1024, "64 KB file must be accepted by the reader.");
            File.AppendAllText(fileName, " ");
            ExpectException<FormatException>(() => TransformationMapFileReader.ReadXml(fileName));
        }
        finally
        {
            File.Delete(fileName);
            File.Delete(textFileName);
            Directory.Delete(directory);
        }
    }

    private static void SerializationPreservesValues()
    {
        var map = TransformationMapXmlSerializer.Parse(CreateXml("0.17200000000000001"));
        var bytes = TransformationMapXmlSerializer.SerializeToUtf8(map);
        Require(!(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF), "UTF-8 must have no BOM.");
        var restored = TransformationMapXmlSerializer.Parse(Encoding.UTF8.GetString(bytes));
        Require(TransformationMapComparer.Compare(map, restored).Count == 0, "Round-trip must preserve all values.");
    }

    private static void NullMatricesAreRejected()
    {
        var map = TransformationMapXmlSerializer.Parse(CreateXml("0"));
        ExpectException<ArgumentNullException>(() => TransformationMapComparer.Compare(null!, map));
        ExpectException<ArgumentNullException>(() => TransformationMapComparer.Compare(map, null!));
    }

    internal static string CreateXml(string value)
    {
        var xml = new StringBuilder("<TransformationMap ");

        for (int row = 1; row <= 4; row++)
        {
            for (int column = 1; column <= 4; column++)
                xml.Append($"m{row}{column}=\"{value}\" ");
        }

        return xml.Append("/>").ToString();
    }

    internal static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    internal static void ExpectException<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}
