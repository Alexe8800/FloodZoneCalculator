using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class HydraulicCalculationIntegrationTests
{
    [Fact]
    public void Runs_profile_01_through_the_base_hydraulic_calculation_chain()
    {
        var source = TestCrossSectionFactory.Symmetric();
        var sourceSnapshot = Snapshot(source);
        var geometry = new CrossSectionGeometryBuilder().Build(source);
        var geometrySnapshot = Snapshot(geometry);

        var result = new CrossSectionGeometryCalculator().Calculate(geometry, 106);
        var omega = Math.Round(result.Area, 6);
        var chi = Math.Round(result.GeometricPerimeterBelowWaterLevel, 6);
        var hydraulicRadius = new HydraulicRadiusCalculator().Calculate(omega, chi);

        const double hydraulicSlope = 0.001;
        const double gravity = 9.81;
        new HydraulicSlopeValidator().Validate(hydraulicSlope);
        var shearVelocity = new ShearVelocityCalculator().Calculate(
            hydraulicRadius,
            hydraulicSlope,
            gravity);

        // V_test is synthetic and is not derived from C, R, If, or vStar.
        const double testVelocity = 2.0;
        var discharge = new DischargeCalculator().Calculate(testVelocity, omega);

        Assert.Equal(426.666667, omega, 6);
        Assert.Equal(94.913058, chi, 6);
        Assert.Equal(426.666667 / 94.913058, hydraulicRadius, 6);
        Assert.Equal(
            Math.Sqrt(9.81 * (426.666667 / 94.913058) * 0.001),
            shearVelocity,
            6);
        Assert.Equal(853.333334, discharge, 6);
        AssertFinite(omega, chi, hydraulicRadius, shearVelocity, discharge);
        Assert.Equal(sourceSnapshot, Snapshot(source));
        Assert.Equal(geometrySnapshot, Snapshot(geometry));
    }

    [Fact]
    public void Runs_profile_02_asymmetric_through_the_base_hydraulic_calculation_chain()
    {
        AssertFiniteScenario(TestCrossSectionFactory.Asymmetric(), 105);
    }

    [Fact]
    public void Runs_profile_03_incomplete_without_adding_points_through_the_base_chain()
    {
        var source = TestCrossSectionFactory.Incomplete();

        AssertFiniteScenario(source, 106);

        var geometry = new CrossSectionGeometryBuilder().Build(source);
        Assert.Equal(5, geometry.Points.Count);
        Assert.Equal(4, geometry.Segments.Count);
    }

    [Fact]
    public void Runs_profile_04_flat_bottom_through_the_base_hydraulic_calculation_chain()
    {
        AssertFiniteScenario(TestCrossSectionFactory.FlatBottom(), 100);
    }

    [Fact]
    public void Runs_profile_05_sloped_bottom_through_the_base_hydraulic_calculation_chain()
    {
        AssertFiniteScenario(TestCrossSectionFactory.SlopedBottom(), 105);
    }

    private static void AssertFiniteScenario(CrossSectionRecord source, double waterLevel)
    {
        var geometry = new CrossSectionGeometryBuilder().Build(source);
        var result = new CrossSectionGeometryCalculator().Calculate(geometry, waterLevel);
        var omega = result.Area;
        var chi = result.GeometricPerimeterBelowWaterLevel;

        AssertFinite(omega, chi);

        var hydraulicRadius = new HydraulicRadiusCalculator().Calculate(omega, chi);
        const double hydraulicSlope = 0.001;
        const double gravity = 9.81;
        new HydraulicSlopeValidator().Validate(hydraulicSlope);
        var shearVelocity = new ShearVelocityCalculator().Calculate(
            hydraulicRadius,
            hydraulicSlope,
            gravity);

        const double testVelocity = 2.0;
        var discharge = new DischargeCalculator().Calculate(testVelocity, omega);

        AssertFinite(hydraulicRadius, shearVelocity, discharge);
    }

    private static void AssertFinite(params double[] values)
    {
        Assert.All(values, value =>
        {
            Assert.False(double.IsNaN(value));
            Assert.False(double.IsInfinity(value));
        });
    }

    private static string[] Snapshot(CrossSectionRecord section) =>
        section.BankPoints
            .Select(point =>
                $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}")
            .ToArray();

    private static string[] Snapshot(CrossSectionGeometry geometry) =>
        geometry.Points
            .Select(point =>
                $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}")
            .Concat(geometry.Segments.Select(segment =>
                $"{segment.Start.PointNumber}->{segment.End.PointNumber}"))
            .ToArray();
}
