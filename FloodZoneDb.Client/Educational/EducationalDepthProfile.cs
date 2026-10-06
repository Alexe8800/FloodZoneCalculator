namespace FloodZoneDb.Client;

public sealed class EducationalDepthProfile
{
    public int CrossSectionNumber { get; }
    public IsodatSide Side { get; }
    public IReadOnlyList<EducationalDepthPoint> Points { get; }
    public IReadOnlyList<EducationalDepthPoint> PointsSortedByX { get; }
    public string Interpretation => IsodatHydraulicPoint.EducationalSyntheticInterpretation;

    public EducationalDepthProfile(
        int crossSectionNumber,
        IsodatSide side,
        IEnumerable<EducationalDepthPoint> points)
    {
        if (crossSectionNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(crossSectionNumber));
        if (!Enum.IsDefined(typeof(IsodatSide), side))
            throw new ArgumentOutOfRangeException(nameof(side));
        if (points == null)
            throw new ArgumentNullException(nameof(points));

        var materialized = points.ToArray();
        if (materialized.Length == 0)
            throw new ArgumentException("Учебный профиль должен содержать хотя бы одну Depth-точку.", nameof(points));
        if (materialized.Any(point => point == null))
            throw new ArgumentException("Профиль содержит пустую Depth-точку.", nameof(points));
        if (materialized.Any(point =>
                point.CrossSectionNumber != crossSectionNumber || point.Side != side))
            throw new ArgumentException("Точки профиля должны принадлежать одному створу и стороне.", nameof(points));

        CrossSectionNumber = crossSectionNumber;
        Side = side;
        Points = Array.AsReadOnly(materialized);
        PointsSortedByX = Array.AsReadOnly(materialized.OrderBy(point => point.X).ToArray());
    }
}

public static class EducationalDepthProfileGrouper
{
    public static IReadOnlyList<EducationalDepthProfile> Group(
        IReadOnlyCollection<IsodatHydraulicPoint> points)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        if (points.Any(point => point == null))
            throw new ArgumentException("Коллекция содержит пустую учебную точку.", nameof(points));

        var profiles = points
            .Where(point => point.DepthH.HasValue)
            .Select(point => new EducationalDepthPoint(
                point.X,
                point.DepthH!.Value,
                point.CrossSectionNumber,
                point.Side,
                point.SourceIsodatId))
            .GroupBy(point => new { point.CrossSectionNumber, point.Side })
            .OrderBy(group => group.Key.CrossSectionNumber)
            .ThenBy(group => group.Key.Side)
            .Select(group => new EducationalDepthProfile(
                group.Key.CrossSectionNumber,
                group.Key.Side,
                group))
            .ToArray();

        return Array.AsReadOnly(profiles);
    }
}
