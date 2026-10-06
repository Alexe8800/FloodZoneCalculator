using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class IsodatHydraulicPointTests
{
    [Fact]
    public void Maps_depth_and_velocity_records_to_separate_educational_points()
    {
        var depth = CreateRecord(21, IsodatType.Depth, 2.75m, 8.5m, "B", 13);
        var velocity = CreateRecord(22, IsodatType.Velocity, 0.8m, 8.5m, "F", 14);
        var durations = new[]
        {
            CreateRecord(23, IsodatType.CalculatedFloodDuration, 12m, 9m, "J", 15),
            CreateRecord(24, IsodatType.ActualFloodDuration, 13m, 9m, "N", 16)
        };
        var input = new[] { depth, velocity }.Concat(durations).ToArray();
        var before = input.Select(Signature).ToArray();

        var mapped = IsodatHydraulicPointMapper.Map(input);

        Assert.Equal(2, mapped.Count);
        Assert.Equal(8.5m, mapped[0].X);
        Assert.Equal(2.75m, mapped[0].DepthH);
        Assert.Null(mapped[0].ObservedVelocity);
        Assert.Equal(8.5m, mapped[1].X);
        Assert.Null(mapped[1].DepthH);
        Assert.Equal(0.8m, mapped[1].ObservedVelocity);
        Assert.Equal(21, mapped[0].SourceIsodatId);
        Assert.Equal(22, mapped[1].SourceIsodatId);
        Assert.Equal(IsodatHydraulicPoint.EducationalSyntheticInterpretation, mapped[0].Interpretation);
        Assert.Equal(before, input.Select(Signature));
        Assert.Equal(
            new[] { IsodatType.CalculatedFloodDuration, IsodatType.ActualFloodDuration },
            durations.Select(record => record.IsodatType));
    }

    [Fact]
    public void Preserves_source_metadata_and_interpretation_status()
    {
        var source = CreateRecord(42, IsodatType.Depth, 4.5m, 17m, "B", 29);
        var point = Assert.Single(IsodatHydraulicPointMapper.Map(new[] { source }));

        Assert.Equal("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            point.SourceFileSha256);
        Assert.Equal("Левый берег", point.SourceSheet);
        Assert.Equal(29, point.SourceRow);
        Assert.Equal("B", point.SourceValueColumn);
        Assert.Equal(IsodatHydraulicPoint.EducationalSyntheticInterpretation, point.Interpretation);
    }

    [Fact]
    public void Keeps_repeated_x_and_does_not_generate_or_interpolate_points()
    {
        var input = new[]
        {
            CreateRecord(1, IsodatType.Depth, 2m, 10m, "B", 10),
            CreateRecord(2, IsodatType.Depth, 4m, 10m, "B", 11),
            CreateRecord(3, IsodatType.Velocity, 0.5m, 20m, "F", 12)
        };

        var points = IsodatHydraulicPointMapper.Map(input);

        Assert.Equal(3, points.Count);
        Assert.Equal(new[] { 10m, 10m, 20m }, points.Select(point => point.X));
        Assert.Equal(new[] { 1L, 2L, 3L }, points.Select(point => point.SourceIsodatId));
    }

    [Fact]
    public void Groups_real_point_records_by_all_sections_and_both_sides_with_statistics()
    {
        var input = (
            from section in new[] { 1, 2, 3 }
            from side in Enum.GetValues<IsodatSide>()
            select new[]
            {
                CreatePoint(section * 100 + (int)side * 10 + 1, section, side, 10m, 2m, null),
                CreatePoint(section * 100 + (int)side * 10 + 2, section, side, 5m, 4m, null),
                CreatePoint(section * 100 + (int)side * 10 + 3, section, side, 5m, null, 0.3m),
                CreatePoint(section * 100 + (int)side * 10 + 4, section, side, 20m, null, 0.9m)
            }).SelectMany(points => points).ToArray();
        var before = input.Select(PointSignature).ToArray();

        var profiles = IsodatHydraulicProfileGrouper.Group(input);

        Assert.Equal(6, profiles.Count);
        foreach (var section in new[] { 1, 2, 3 })
        foreach (var side in Enum.GetValues<IsodatSide>())
        {
            var profile = Assert.Single(profiles, item =>
                item.CrossSectionNumber == section && item.Side == side);
            Assert.Equal(4, profile.Points.Count);
            Assert.Equal(5m, profile.MinX);
            Assert.Equal(20m, profile.MaxX);
            Assert.Equal(2m, profile.MinDepth);
            Assert.Equal(4m, profile.MaxDepth);
            Assert.Equal(0.3m, profile.MinObservedVelocity);
            Assert.Equal(0.9m, profile.MaxObservedVelocity);
            Assert.Equal(
                IsodatHydraulicPoint.EducationalSyntheticInterpretation,
                profile.Interpretation);
        }

        Assert.Equal(before, input.Select(PointSignature));
    }

    [Fact]
    public void Profile_statistics_allow_missing_depth_or_velocity_and_points_are_not_sorted_in_place()
    {
        var input = new[]
        {
            CreatePoint(1, 1, IsodatSide.Left, 9m, null, 0.7m),
            CreatePoint(2, 1, IsodatSide.Left, 3m, null, 0.2m),
            CreatePoint(3, 1, IsodatSide.Left, 3m, 4m, null)
        };

        var profile = Assert.Single(IsodatHydraulicProfileGrouper.Group(input));

        Assert.Equal(new[] { 9m, 3m, 3m }, profile.Points.Select(point => point.X));
        Assert.Equal(new[] { 3m, 3m, 9m }, profile.PointsSortedByX.Select(point => point.X));
        Assert.Equal(4m, profile.MinDepth);
        Assert.Equal(4m, profile.MaxDepth);
        Assert.Equal(0.2m, profile.MinObservedVelocity);
        Assert.Equal(0.7m, profile.MaxObservedVelocity);
    }

    [Fact]
    public void Empty_input_returns_no_profiles_and_repeated_grouping_is_deterministic()
    {
        Assert.Empty(IsodatHydraulicProfileGrouper.Group(Array.Empty<IsodatHydraulicPoint>()));

        var points = new[]
        {
            CreatePoint(1, 2, IsodatSide.Right, 5m, 3m, null),
            CreatePoint(2, 1, IsodatSide.Left, 7m, null, 0.5m)
        };
        var first = IsodatHydraulicProfileGrouper.Group(points);
        var second = IsodatHydraulicProfileGrouper.Group(points);

        Assert.Equal(ProfileSignature(first), ProfileSignature(second));
    }

    [Fact]
    public void Mapping_requires_persisted_source_ids()
    {
        var record = CreateRecord(0, IsodatType.Depth, 3m, 7m, "B", 5);

        Assert.Throws<ArgumentException>(() => IsodatHydraulicPointMapper.Map(new[] { record }));
    }

    private static IsodatRecord CreateRecord(
        long id,
        IsodatType type,
        decimal value,
        decimal distance,
        string valueColumn,
        int row) =>
        new()
        {
            Id = id,
            CrossSectionNumber = 1 + (int)(id % 3),
            Side = IsodatSide.Left,
            IsodatType = type,
            Value = value,
            DistanceM = distance,
            SourceSheet = "Левый берег",
            SourceFileName = "source.xlsx",
            SourceFileSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            SourceRow = row,
            ValueSourceColumn = valueColumn,
            DistanceSourceColumn = "C"
        };

    private static IsodatHydraulicPoint CreatePoint(
        long sourceId,
        int section,
        IsodatSide side,
        decimal x,
        decimal? depth,
        decimal? velocity) =>
        new(
            sourceId,
            section,
            side,
            x,
            depth,
            velocity,
            sourceId,
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            side == IsodatSide.Left ? "Левый берег" : "Правый берег",
            (int)sourceId,
            depth.HasValue ? "B" : "F");

    private static string Signature(IsodatRecord record) =>
        $"{record.Id}|{record.Value}|{record.DistanceM}|{record.IsodatType}|{record.SourceRow}";

    private static string PointSignature(IsodatHydraulicPoint point) =>
        $"{point.SourceIsodatId}|{point.CrossSectionNumber}|{point.Side}|{point.X}|"
        + $"{point.DepthH}|{point.ObservedVelocity}|{point.Interpretation}";

    private static string ProfileSignature(IEnumerable<IsodatHydraulicProfile> profiles) =>
        string.Join(
            ";",
            profiles.Select(profile =>
                $"{profile.CrossSectionNumber}|{profile.Side}|{string.Join(",", profile.Points.Select(PointSignature))}"));
}
