namespace FloodZoneDb.Client;

public sealed class EducationalDepthPoint
{
    public decimal X { get; }
    public decimal DepthH { get; }
    public int CrossSectionNumber { get; }
    public IsodatSide Side { get; }
    public long SourceIsodatId { get; }

    public EducationalDepthPoint(
        decimal x,
        decimal depthH,
        int crossSectionNumber,
        IsodatSide side,
        long sourceIsodatId)
    {
        if (depthH < 0)
            throw new ArgumentOutOfRangeException(nameof(depthH), "Учебная глубина не может быть отрицательной.");
        if (crossSectionNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(crossSectionNumber));
        if (!Enum.IsDefined(typeof(IsodatSide), side))
            throw new ArgumentOutOfRangeException(nameof(side));
        if (sourceIsodatId <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceIsodatId));

        X = x;
        DepthH = depthH;
        CrossSectionNumber = crossSectionNumber;
        Side = side;
        SourceIsodatId = sourceIsodatId;
    }
}
