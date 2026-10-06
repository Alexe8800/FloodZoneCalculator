using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class CrossSectionGeometryBuilderTests
{
    public static IEnumerable<object[]> Profiles =>
    [
        ["TEST_PROFILE_01_SYMMETRIC", TestCrossSectionFactory.Symmetric()],
        ["TEST_PROFILE_02_ASYMMETRIC", TestCrossSectionFactory.Asymmetric()],
        ["TEST_PROFILE_03_INCOMPLETE", TestCrossSectionFactory.Incomplete()],
        ["TEST_PROFILE_04_FLAT_BOTTOM", TestCrossSectionFactory.FlatBottom()],
        ["TEST_PROFILE_05_SLOPED_BOTTOM", TestCrossSectionFactory.SlopedBottom()]
    ];

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Builds_each_synthetic_profile_without_creating_points(
        string profileName,
        CrossSectionRecord source)
    {
        Assert.StartsWith("TEST_PROFILE_", profileName);
        var original = source.BankPoints.Select(ToSnapshot).ToList();

        var geometry = new CrossSectionGeometryBuilder().Build(source);

        Assert.Equal(source.BankPoints.Count, geometry.OrderedPoints.Count);
        Assert.Equal(geometry.OrderedPoints.Count - 1, geometry.Segments.Count);
        Assert.Equal(original, source.BankPoints.Select(ToSnapshot).ToList());

        Assert.Equal(
            source.BankPoints.Select(point => double.Parse(point.DistanceM, System.Globalization.CultureInfo.InvariantCulture)).OrderBy(value => value),
            geometry.OrderedPoints.Select(point => point.DistanceM));
        Assert.Equal(
            source.BankPoints.OrderBy(point => double.Parse(point.DistanceM, System.Globalization.CultureInfo.InvariantCulture))
                .Select(point => double.Parse(point.ElevationM, System.Globalization.CultureInfo.InvariantCulture)),
            geometry.OrderedPoints.Select(point => point.ElevationM));
    }

    [Fact]
    public void Builds_symmetric_profile_with_nine_points_and_eight_segments()
    {
        var geometry = new CrossSectionGeometryBuilder().Build(TestCrossSectionFactory.Symmetric());

        Assert.Equal(9, geometry.OrderedPoints.Count);
        Assert.Equal(8, geometry.Segments.Count);
        Assert.Equal(0, geometry.OrderedPoints[0].DistanceM);
        Assert.Equal(110, geometry.OrderedPoints[0].ElevationM);
        Assert.Equal(160, geometry.OrderedPoints[^1].DistanceM);
        Assert.Equal(110, geometry.OrderedPoints[^1].ElevationM);
        Assert.Contains(geometry.OrderedPoints, point =>
            point.Side == "Center" &&
            point.PointType == "Bottom" &&
            point.DistanceM == 80 &&
            point.ElevationM == 98);
    }

    [Fact]
    public void Builds_incomplete_profile_with_only_five_real_points()
    {
        var geometry = new CrossSectionGeometryBuilder().Build(TestCrossSectionFactory.Incomplete());

        Assert.Equal(5, geometry.OrderedPoints.Count);
        Assert.Equal(4, geometry.Segments.Count);
        Assert.DoesNotContain(geometry.OrderedPoints, point => point.PointType == "ChannelBank");
        Assert.DoesNotContain(geometry.OrderedPoints, point =>
            point.PointType == "Bottom" && point.Side != "Center");
    }

    [Fact]
    public void Orders_points_by_distance_even_when_input_is_not_ordered()
    {
        var source = TestCrossSectionFactory.SlopedBottom();
        source.BankPoints.Reverse();

        var geometry = new CrossSectionGeometryBuilder().Build(source);

        Assert.Equal(
            source.BankPoints
                .Select(point => double.Parse(point.DistanceM, System.Globalization.CultureInfo.InvariantCulture))
                .OrderBy(distance => distance),
            geometry.OrderedPoints.Select(point => point.DistanceM));
    }

    private static string ToSnapshot(BankPointRecord point) =>
        $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}";
}
