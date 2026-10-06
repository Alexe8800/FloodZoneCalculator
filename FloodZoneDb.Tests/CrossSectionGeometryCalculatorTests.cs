using FloodZoneCalculator.Domain;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class CrossSectionGeometryCalculatorTests
{
    [Theory]
    [InlineData(98, 0, 0)]
    [InlineData(105, 340, 81.430559)]
    [InlineData(106, 426.666667, 94.913058)]
    [InlineData(110, 920, 162.077558)]
    public void Calculates_symmetric_profile_below_water_level(
        double waterLevel,
        double expectedArea,
        double expectedPerimeter)
    {
        var geometry = Build(TestCrossSectionFactory.Symmetric());

        var result = new CrossSectionGeometryCalculator().Calculate(geometry, waterLevel);

        Assert.Equal(waterLevel, result.WaterLevel);
        Assert.Equal(expectedArea, result.Area, 6);
        Assert.Equal(expectedPerimeter, result.GeometricPerimeterBelowWaterLevel, 6);
    }

    [Fact]
    public void Calculates_incomplete_profile_using_only_existing_segments()
    {
        var geometry = Build(TestCrossSectionFactory.Incomplete());

        var result = new CrossSectionGeometryCalculator().Calculate(geometry, 106);

        Assert.Equal(4, geometry.Segments.Count);
        Assert.Equal(400, result.Area, 6);
        Assert.Equal(101.323140, result.GeometricPerimeterBelowWaterLevel, 6);
    }

    [Fact]
    public void Handles_flat_bottom_without_division_by_zero()
    {
        var geometry = Build(TestCrossSectionFactory.FlatBottom());

        var result = new CrossSectionGeometryCalculator().Calculate(geometry, 100);

        Assert.Equal(0, result.Area, 6);
        Assert.Equal(40, result.GeometricPerimeterBelowWaterLevel, 6);
    }

    [Fact]
    public void Handles_asymmetric_and_sloped_profiles_without_assuming_symmetry()
    {
        var calculator = new CrossSectionGeometryCalculator();

        var asymmetric = calculator.Calculate(Build(TestCrossSectionFactory.Asymmetric()), 105);
        var sloped = calculator.Calculate(Build(TestCrossSectionFactory.SlopedBottom()), 105);

        Assert.Equal(335.5, asymmetric.Area, 6);
        Assert.Equal(96.872210, asymmetric.GeometricPerimeterBelowWaterLevel, 6);
        Assert.Equal(401.125, sloped.Area, 6);
        Assert.Equal(95.539718, sloped.GeometricPerimeterBelowWaterLevel, 6);
    }

    [Fact]
    public void Does_not_modify_geometry_or_source_records()
    {
        var source = TestCrossSectionFactory.Symmetric();
        var sourceSnapshot = source.BankPoints.Select(point =>
            $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}").ToList();
        var geometry = Build(source);
        var pointSnapshot = geometry.OrderedPoints.Select(point =>
            $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}").ToList();
        var segmentCount = geometry.Segments.Count;

        _ = new CrossSectionGeometryCalculator().Calculate(geometry, 106);

        Assert.Equal(sourceSnapshot, source.BankPoints.Select(point =>
            $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}"));
        Assert.Equal(pointSnapshot, geometry.OrderedPoints.Select(point =>
            $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}"));
        Assert.Equal(segmentCount, geometry.Segments.Count);
    }

    private static CrossSectionGeometry Build(FloodZoneDb.Client.CrossSectionRecord section) =>
        new CrossSectionGeometryBuilder().Build(section);
}
