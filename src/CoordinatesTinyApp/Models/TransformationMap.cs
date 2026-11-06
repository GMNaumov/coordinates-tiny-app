namespace CoordinatesTinyApp.Models;

public sealed class TransformationMap
{
    private const int MatrixSize = 4;

    private readonly double[,] _values;

    public TransformationMap(double[,] values)
    {
        if (values is null)
            throw new ArgumentNullException(nameof(values));

        if (values.GetLength(0) != MatrixSize || values.GetLength(1) != MatrixSize)
            throw new ArgumentException("Матрица преобразования должна иметь размер 4 × 4.", nameof(values));

        _values = (double[,])values.Clone();

        for (int row = 0; row < MatrixSize; row++)
        {
            for (int column = 0; column < MatrixSize; column++)
            {
                if (!Double.IsFinite(_values[row, column]))
                    throw new ArgumentException("Все элементы матрицы должны быть конечными числами.", nameof(values));
            }
        }
    }

    public double this[int row, int column] => _values[row, column];

    public double TranslationX => _values[3, 0];

    public double TranslationY => _values[3, 1];

    public double TranslationZ => _values[3, 2];
}
