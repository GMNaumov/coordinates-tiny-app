using CoordinatesTinyApp.Services;

namespace CoordinatesTinyApp.Models;

internal sealed class CoordinateRecord
{
    public int? ObjectId { get; init; }

    public Guid? ObjectGuid { get; init; }

    public string? Placement { get; init; }

    public bool IsPlacementTooLarge { get; init; }

    public string PlacementDisplay => IsPlacementTooLarge ? "Матрица превышает 64 КБ" : Placement ?? "(NULL)";

    public string GetValidatedPlacement()
    {
        if (IsPlacementTooLarge)
            throw new FormatException("Матрица выбранной записи превышает 64 КБ.");

        string xml = Placement ?? "";
        TransformationMapXmlSerializer.Parse(xml);
        return xml;
    }
}
