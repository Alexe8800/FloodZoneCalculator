namespace FloodZoneDb.Client;

public sealed class IsodatHydraulicProfile
{
    public int CrossSectionNumber { get; }
    public IsodatSide Side { get; }
    public IReadOnlyList<IsodatHydraulicPoint> Points { get; }
    public IReadOnlyList<IsodatHydraulicPoint> PointsSortedByX { get; }
    public decimal MinX { get; }
    public decimal MaxX { get; }
    public decimal? MinDepth { get; }
    public decimal? MaxDepth { get; }
    public decimal? MinObservedVelocity { get; }
    public decimal? MaxObservedVelocity { get; }
    public string Interpretation => IsodatHydraulicPoint.EducationalSyntheticInterpretation;

    internal IsodatHydraulicProfile(
        int crossSectionNumber,
        IsodatSide side,
        IEnumerable<IsodatHydraulicPoint> points)
    {
        CrossSectionNumber = crossSectionNumber;
        Side = side;
        var materialized = points.ToArray();
        if (materialized.Length == 0)
            throw new ArgumentException("Учебный профиль должен содержать хотя бы одну точку.", nameof(points));

        Points = Array.AsReadOnly(materialized);
        PointsSortedByX = Array.AsReadOnly(materialized.OrderBy(point => point.X).ToArray());
        MinX = materialized.Min(point => point.X);
        MaxX = materialized.Max(point => point.X);

        var depths = materialized
            .Where(point => point.DepthH.HasValue)
            .Select(point => point.DepthH!.Value)
            .ToArray();
        MinDepth = depths.Length == 0 ? null : depths.Min();
        MaxDepth = depths.Length == 0 ? null : depths.Max();

        var velocities = materialized
            .Where(point => point.ObservedVelocity.HasValue)
            .Select(point => point.ObservedVelocity!.Value)
            .ToArray();
        MinObservedVelocity = velocities.Length == 0 ? null : velocities.Min();
        MaxObservedVelocity = velocities.Length == 0 ? null : velocities.Max();
    }
}

public static class IsodatHydraulicProfileGrouper
{
    public static IReadOnlyList<IsodatHydraulicProfile> Group(
        IReadOnlyList<IsodatHydraulicPoint> points)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));

        if (points.Any(point => point == null))
            throw new ArgumentException("Коллекция содержит пустую учебную точку.", nameof(points));

        var profiles = points
            .GroupBy(point => new { point.CrossSectionNumber, point.Side })
            .OrderBy(group => group.Key.CrossSectionNumber)
            .ThenBy(group => group.Key.Side)
            .Select(group => new IsodatHydraulicProfile(
                group.Key.CrossSectionNumber,
                group.Key.Side,
                group))
            .ToArray();
        return Array.AsReadOnly(profiles);
    }
}
