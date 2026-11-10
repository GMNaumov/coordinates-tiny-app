namespace CoordinatesTinyApp.Models;

public sealed class TransformationMapDifference
{
    public TransformationMapDifference(string parameterName, double firstValue, double secondValue)
    {
        ParameterName = parameterName;
        FirstValue = firstValue;
        SecondValue = secondValue;
    }

    public string ParameterName { get; }

    public double FirstValue { get; }

    public double SecondValue { get; }

    // Разность конечных значений может выйти за диапазон double.
    public double? Difference
    {
        get
        {
            double difference = SecondValue - FirstValue;
            return Double.IsFinite(difference) ? difference : null;
        }
    }
}
