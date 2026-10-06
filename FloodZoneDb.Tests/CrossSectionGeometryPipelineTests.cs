using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class CrossSectionGeometryPipelineTests
{
    [Fact]
    public void Runs_symmetric_profile_through_factory_builder_and_calculator()
    {
        var source = TestCrossSectionFactory.Symmetric();
        var sourceSnapshot = Snapshot(source);

        var geometry = new CrossSectionGeometryBuilder().Build(source);
        var result = new CrossSectionGeometryCalculator().Calculate(geometry, 106);

        Assert.Equal(9, geometry.Points.Count);
        Assert.Equal(8, geometry.Segments.Count);
        Assert.Equal(0, geometry.Points[0].DistanceM);
        Assert.Equal(110, geometry.Points[0].ElevationM);
        Assert.Equal(160, geometry.Points[^1].DistanceM);
        Assert.Equal(110, geometry.Points[^1].ElevationM);
        Assert.Equal(426.666667, result.Area, 6);
        Assert.Equal(94.913058, result.GeometricPerimeterBelowWaterLevel, 6);
        var hydraulicRadius = new HydraulicRadiusCalculator().Calculate(
            result.Area,
            result.GeometricPerimeterBelowWaterLevel);
        Assert.Equal(426.666667 / 94.913058, hydraulicRadius, 6);
        Assert.False(double.IsNaN(hydraulicRadius));
        Assert.False(double.IsInfinity(hydraulicRadius));

        // If is a synthetic test slope and is not imported from the real methodology.
        const double syntheticHydraulicSlope = 0.001;
        var shearVelocity = new ShearVelocityCalculator().Calculate(
            hydraulicRadius,
            syntheticHydraulicSlope,
            9.81);
        var expectedShearVelocity = Math.Sqrt(
            9.81 * (426.666667 / 94.913058) * syntheticHydraulicSlope);
        Assert.Equal(expectedShearVelocity, shearVelocity, 6);
        Assert.False(double.IsNaN(shearVelocity));
        Assert.False(double.IsInfinity(shearVelocity));
        Assert.Equal(sourceSnapshot, Snapshot(source));
    }

    [Fact]
    public void Runs_incomplete_profile_without_adding_points_or_segments()
    {
        var source = TestCrossSectionFactory.Incomplete();
        var geometry = new CrossSectionGeometryBuilder().Build(source);

        var result = new CrossSectionGeometryCalculator().Calculate(geometry, 106);

        Assert.Equal(5, geometry.Points.Count);
        Assert.Equal(4, geometry.Segments.Count);
        Assert.Equal(400, result.Area, 6);
        Assert.Equal(101.323140, result.GeometricPerimeterBelowWaterLevel, 6);
        AssertFiniteRadius(result);
        Assert.DoesNotContain(geometry.Points, point => point.PointType == "ChannelBank");
        Assert.Equal(5, source.BankPoints.Count);
    }

    [Fact]
    public void Runs_symmetric_profile_to_discharge_with_synthetic_velocity()
    {
        var source = TestCrossSectionFactory.Symmetric();
        var geometry = new CrossSectionGeometryBuilder().Build(source);
        var result = new CrossSectionGeometryCalculator().Calculate(geometry, 106);

        Assert.Equal(426.666667, result.Area, 6);
        var omega = Math.Round(result.Area, 6);

        // V = 2.0 is a synthetic test value and is not imported from the real methodology.
        const double syntheticVelocity = 2.0;
        var discharge = new DischargeCalculator().Calculate(
            syntheticVelocity,
            omega);
        var expectedDischarge = 2.0 * 426.666667;

        Assert.Equal(expectedDischarge, discharge, 6);
        Assert.False(double.IsNaN(discharge));
        Assert.False(double.IsInfinity(discharge));
    }

    [Fact]
    public void Runs_asymmetric_profile_without_assuming_symmetry()
    {
        var source = TestCrossSectionFactory.Asymmetric();
        var geometry = new CrossSectionGeometryBuilder().Build(source);

        var result = new CrossSectionGeometryCalculator().Calculate(geometry, 105);

        Assert.Equal(9, geometry.Points.Count);
        Assert.Equal(8, geometry.Segments.Count);
        Assert.False(double.IsNaN(result.Area));
        Assert.False(double.IsInfinity(result.Area));
        Assert.False(double.IsNaN(result.GeometricPerimeterBelowWaterLevel));
        Assert.False(double.IsInfinity(result.GeometricPerimeterBelowWaterLevel));
        AssertFiniteRadius(result);
    }

    [Fact]
    public void Runs_sloped_bottom_profile_using_actual_elevations()
    {
        var source = TestCrossSectionFactory.SlopedBottom();
        var geometry = new CrossSectionGeometryBuilder().Build(source);
        var elevationsBefore = geometry.Points.Select(point => point.ElevationM).ToArray();

        var result = new CrossSectionGeometryCalculator().Calculate(geometry, 105);

        Assert.Equal(9, geometry.Points.Count);
        Assert.Equal(8, geometry.Segments.Count);
        Assert.Equal(401.125, result.Area, 6);
        Assert.Equal(95.539718, result.GeometricPerimeterBelowWaterLevel, 6);
        Assert.Equal(elevationsBefore, geometry.Points.Select(point => point.ElevationM));
        Assert.Equal(98, geometry.Points.Single(point => point.Side == "Center").ElevationM);
        Assert.Equal(99, geometry.Points.Single(point => point.Side == "Right" && point.PointType == "Bottom").ElevationM);
        AssertFiniteRadius(result);
    }

    [Fact]
    public void Runs_flat_bottom_profile_through_hydraulic_radius()
    {
        var source = TestCrossSectionFactory.FlatBottom();
        var geometry = new CrossSectionGeometryBuilder().Build(source);
        var result = new CrossSectionGeometryCalculator().Calculate(geometry, 100);

        Assert.Equal(9, geometry.Points.Count);
        Assert.Equal(8, geometry.Segments.Count);
        Assert.Equal(0, result.Area, 6);
        Assert.Equal(40, result.GeometricPerimeterBelowWaterLevel, 6);
        Assert.Equal(0, new HydraulicRadiusCalculator().Calculate(
            result.Area,
            result.GeometricPerimeterBelowWaterLevel), 6);
    }

    private static void AssertFiniteRadius(CrossSectionGeometryCalculationResult result)
    {
        var radius = new HydraulicRadiusCalculator().Calculate(
            result.Area,
            result.GeometricPerimeterBelowWaterLevel);
        Assert.False(double.IsNaN(radius));
        Assert.False(double.IsInfinity(radius));
    }

    private static string[] Snapshot(CrossSectionRecord section) =>
        section.BankPoints
            .Select(point =>
                $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}")
            .ToArray();
}
