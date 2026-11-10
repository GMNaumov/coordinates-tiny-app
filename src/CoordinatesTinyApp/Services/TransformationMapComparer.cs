using CoordinatesTinyApp.Models;

namespace CoordinatesTinyApp.Services;

internal static class TransformationMapComparer
{
    private const int MatrixSize = 4;

    public static IReadOnlyList<TransformationMapDifference> Compare(TransformationMap firstMap, TransformationMap secondMap)
    {
        ArgumentNullException.ThrowIfNull(firstMap);
        ArgumentNullException.ThrowIfNull(secondMap);

        var differences = new List<TransformationMapDifference>();

        // Сравниваем числовые значения без допуска; положительный и отрицательный ноль равны.
        for (int row = 0; row < MatrixSize; row++)
        {
            for (int column = 0; column < MatrixSize; column++)
            {
                double firstValue = firstMap[row, column];
                double secondValue = secondMap[row, column];

                if (firstValue != secondValue)
                {
                    differences.Add(new TransformationMapDifference(
                        $"m{row + 1}{column + 1}", firstValue, secondValue));
                }
            }
        }

        return differences.AsReadOnly();
    }
}
