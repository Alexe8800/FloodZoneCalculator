using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class FullHydraulicChainAuditTests
{
    private const double WaterLevel = 106;
    private const double Gravity = 9.81;
    private const double KinematicViscosity = 1.15e-6;
    private const double Depth = 5;
    private const double Width = 50;
    private const double HydraulicSlope = 0.001;

    [Fact]
    public void Profile_01_direct_calculators_match_orchestrator()
    {
        var source = TestCrossSectionFactory.Symmetric();
        var sourceSnapshot = Snapshot(source);
        var geometry = new CrossSectionGeometryBuilder().Build(source);
        var geometrySnapshot = Snapshot(geometry);

        var direct = CalculateDirect(geometry, WaterLevel);
        var orchestrated = CalculateOrchestrated(geometry, WaterLevel);

        AssertResultsMatch(direct, orchestrated);
        AssertClose(426.666667, orchestrated.Omega, 1e-6);
        AssertClose(94.913058, orchestrated.Chi, 1e-6);
        AssertClose(4.495342116150, orchestrated.HydraulicRadius, 2e-8);
        AssertClose(0.209998347992, orchestrated.ShearVelocity, 2e-9);
        AssertClose(0.08021980204997178, orchestrated.ChezyCoefficient, 1e-14);
        AssertClose(0.005378522138334274, orchestrated.Velocity, 2e-10);
        AssertClose(2.2948361141487976, orchestrated.Discharge, 2e-8);

        Assert.Equal(sourceSnapshot, Snapshot(source));
        Assert.Equal(geometrySnapshot, Snapshot(geometry));
    }

    [Fact]
    public void Profile_02_asymmetric_direct_calculators_match_orchestrator()
    {
        var source = TestCrossSectionFactory.Asymmetric();
        var geometry = new CrossSectionGeometryBuilder().Build(source);

        var direct = CalculateDirect(geometry, 105);
        var orchestrated = CalculateOrchestrated(geometry, 105);

        AssertResultsMatch(direct, orchestrated);
        AssertNonNegativeFinite(orchestrated);
    }

    [Fact]
    public void Profile_03_incomplete_direct_calculators_match_orchestrator_without_adding_geometry()
    {
        var source = TestCrossSectionFactory.Incomplete();
        var sourceSnapshot = Snapshot(source);
        var geometry = new CrossSectionGeometryBuilder().Build(source);
        var geometrySnapshot = Snapshot(geometry);

        Assert.Equal(5, geometry.Points.Count);
        Assert.Equal(4, geometry.Segments.Count);

        var direct = CalculateDirect(geometry, WaterLevel);
        var orchestrated = CalculateOrchestrated(geometry, WaterLevel);

        AssertResultsMatch(direct, orchestrated);
        AssertNonNegativeFinite(orchestrated);
        Assert.Equal(5, geometry.Points.Count);
        Assert.Equal(4, geometry.Segments.Count);
        Assert.Equal(sourceSnapshot, Snapshot(source));
        Assert.Equal(geometrySnapshot, Snapshot(geometry));
    }

    private static CrossSectionHydraulicResult CalculateOrchestrated(
        CrossSectionGeometry geometry,
        double waterLevel) =>
        new CrossSectionHydraulicResultCalculator().Calculate(
            geometry,
            waterLevel,
            Gravity,
            KinematicViscosity,
            Depth,
            Width,
            HydraulicSlope);

    private static DirectHydraulicValues CalculateDirect(
        CrossSectionGeometry geometry,
        double waterLevel)
    {
        var geometryResult = new CrossSectionGeometryCalculator().Calculate(geometry, waterLevel);
        var omega = geometryResult.Area;
        var chi = geometryResult.GeometricPerimeterBelowWaterLevel;
        var hydraulicRadius = new HydraulicRadiusCalculator().Calculate(omega, chi);

        new HydraulicSlopeValidator().Validate(HydraulicSlope);
        var shearVelocity = new ShearVelocityCalculator().Calculate(
            hydraulicRadius,
            HydraulicSlope,
            Gravity);
        var chezyCoefficient = GrishaninChezyCalculator.Calculate(
            Gravity,
            KinematicViscosity,
            Depth,
            Width);
        var velocity = ChezyVelocityCalculator.Calculate(
            chezyCoefficient,
            hydraulicRadius,
            HydraulicSlope);
        var discharge = new DischargeCalculator().Calculate(velocity, omega);

        return new DirectHydraulicValues(
            waterLevel,
            omega,
            chi,
            hydraulicRadius,
            HydraulicSlope,
            shearVelocity,
            chezyCoefficient,
            velocity,
            discharge);
    }

    private static void AssertResultsMatch(
        DirectHydraulicValues direct,
        CrossSectionHydraulicResult orchestrated)
    {
        AssertClose(direct.WaterLevel, orchestrated.WaterLevel, 0);
        AssertClose(direct.Omega, orchestrated.Omega, 1e-12);
        AssertClose(direct.Chi, orchestrated.Chi, 1e-12);
        AssertClose(direct.HydraulicRadius, orchestrated.HydraulicRadius, 1e-12);
        AssertClose(direct.HydraulicSlope, orchestrated.HydraulicSlope, 0);
        AssertClose(direct.ShearVelocity, orchestrated.ShearVelocity, 1e-12);
        AssertClose(direct.ChezyCoefficient, orchestrated.ChezyCoefficient, 1e-12);
        AssertClose(direct.Velocity, orchestrated.Velocity, 1e-12);
        AssertClose(direct.Discharge, orchestrated.Discharge, 1e-12);
    }

    private static void AssertNonNegativeFinite(CrossSectionHydraulicResult result)
    {
        AssertFinite(
            result.Omega,
            result.Chi,
            result.HydraulicRadius,
            result.HydraulicSlope,
            result.ShearVelocity,
            result.ChezyCoefficient,
            result.Velocity,
            result.Discharge);
        Assert.True(result.Omega >= 0);
        Assert.True(result.Chi >= 0);
        Assert.True(result.HydraulicRadius >= 0);
        Assert.True(result.ShearVelocity >= 0);
        Assert.True(result.Velocity >= 0);
        Assert.True(result.Discharge >= 0);
    }

    private static void AssertFinite(params double[] values)
    {
        Assert.All(values, value =>
        {
            Assert.False(double.IsNaN(value));
            Assert.False(double.IsInfinity(value));
        });
    }

    private static void AssertClose(double expected, double actual, double tolerance)
    {
        Assert.True(
            Math.Abs(expected - actual) <= tolerance,
            $"Expected {expected:R}, actual {actual:R}, tolerance {tolerance:R}.");
    }

    private static string[] Snapshot(CrossSectionRecord section) =>
        section.BankPoints
            .Select(point =>
                $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}")
            .ToArray();

    private static string[] Snapshot(CrossSectionGeometry geometry) =>
        geometry.Points
            .Select(point =>
                $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM:R}|{point.ElevationM:R}")
            .Concat(geometry.Segments.Select(segment =>
                $"{segment.Start.PointNumber}->{segment.End.PointNumber}"))
            .ToArray();

    private sealed record DirectHydraulicValues(
        double WaterLevel,
        double Omega,
        double Chi,
        double HydraulicRadius,
        double HydraulicSlope,
        double ShearVelocity,
        double ChezyCoefficient,
        double Velocity,
        double Discharge);
}
