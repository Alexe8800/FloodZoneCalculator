using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class MultiCrossSectionHydraulicCalculationTests
{
    private const double Gravity = 9.81;
    private const double KinematicViscosity = 1.15e-6;
    private const double Depth = 5;
    private const double Width = 50;
    private const double HydraulicSlope = 0.001;

    [Fact]
    public void Calculates_all_five_profiles_independently_and_preserves_results()
    {
        var profiles = new (string Name, Func<CrossSectionRecord> Factory)[]
        {
            ("PROFILE_01_SYMMETRIC", TestCrossSectionFactory.Symmetric),
            ("PROFILE_02_ASYMMETRIC", TestCrossSectionFactory.Asymmetric),
            ("PROFILE_03_INCOMPLETE", TestCrossSectionFactory.Incomplete),
            ("PROFILE_04_FLAT_BOTTOM", TestCrossSectionFactory.FlatBottom),
            ("PROFILE_05_SLOPED_BOTTOM", TestCrossSectionFactory.SlopedBottom)
        };
        var results = new List<CrossSectionHydraulicResult>();
        var snapshots = new List<string[]>();
        var geometrySnapshots = new List<string[]>();

        foreach (var profile in profiles)
        {
            var source = profile.Factory();
            var geometry = new CrossSectionGeometryBuilder().Build(source);
            snapshots.Add(Snapshot(source));
            geometrySnapshots.Add(Snapshot(geometry));

            var result = Calculate(geometry, 106);

            AssertResultIsValid(result);
            results.Add(result);
        }

        Assert.Equal(5, results.Count);

        for (var index = 0; index < profiles.Length; index++)
        {
            var source = profiles[index].Factory();
            var geometry = new CrossSectionGeometryBuilder().Build(source);
            var repeated = Calculate(geometry, 106);

            AssertResultsEqual(results[index], repeated);
            Assert.Equal(snapshots[index], Snapshot(source));
            Assert.Equal(geometrySnapshots[index], Snapshot(geometry));
        }
    }

    [Fact]
    public void Profile_01_supports_independent_water_levels_without_mutating_geometry()
    {
        var source = TestCrossSectionFactory.Symmetric();
        var sourceSnapshot = Snapshot(source);
        var geometry = new CrossSectionGeometryBuilder().Build(source);
        var geometrySnapshot = Snapshot(geometry);
        var calculator = new CrossSectionHydraulicResultCalculator();

        var result105 = Calculate(calculator, geometry, 105);
        var result106 = Calculate(calculator, geometry, 106);
        var result110 = Calculate(calculator, geometry, 110);

        Assert.Equal(105, result105.WaterLevel);
        Assert.Equal(106, result106.WaterLevel);
        Assert.Equal(110, result110.WaterLevel);
        AssertResultIsValid(result105);
        AssertResultIsValid(result106);
        AssertResultIsValid(result110);
        Assert.True(
            result105.Omega != result106.Omega ||
            result106.Omega != result110.Omega);
        Assert.Equal(sourceSnapshot, Snapshot(source));
        Assert.Equal(geometrySnapshot, Snapshot(geometry));
        Assert.Equal(9, source.BankPoints.Count);
        Assert.Equal(9, geometry.Points.Count);
    }

    [Fact]
    public void Profile_03_remains_incomplete_after_multi_profile_calculations()
    {
        var source = TestCrossSectionFactory.Incomplete();
        var sourceSnapshot = Snapshot(source);
        var geometry = new CrossSectionGeometryBuilder().Build(source);
        var geometrySnapshot = Snapshot(geometry);

        Assert.Equal(5, geometry.Points.Count);
        Assert.Equal(4, geometry.Segments.Count);

        _ = Calculate(geometry, 106);
        _ = Calculate(geometry, 105);
        _ = Calculate(geometry, 110);

        Assert.Equal(5, geometry.Points.Count);
        Assert.Equal(4, geometry.Segments.Count);
        Assert.Equal(sourceSnapshot, Snapshot(source));
        Assert.Equal(geometrySnapshot, Snapshot(geometry));
    }

    [Fact]
    public void Recalculating_each_profile_produces_the_same_result()
    {
        var profiles = new Func<CrossSectionRecord>[]
        {
            TestCrossSectionFactory.Symmetric,
            TestCrossSectionFactory.Asymmetric,
            TestCrossSectionFactory.Incomplete,
            TestCrossSectionFactory.FlatBottom,
            TestCrossSectionFactory.SlopedBottom
        };

        foreach (var factory in profiles)
        {
            var firstSource = factory();
            var firstGeometry = new CrossSectionGeometryBuilder().Build(firstSource);
            var first = Calculate(firstGeometry, 106);

            var secondSource = factory();
            var secondGeometry = new CrossSectionGeometryBuilder().Build(secondSource);
            var second = Calculate(secondGeometry, 106);

            AssertResultsEqual(first, second);
        }
    }

    private static CrossSectionHydraulicResult Calculate(
        CrossSectionGeometry geometry,
        double waterLevel) =>
        Calculate(new CrossSectionHydraulicResultCalculator(), geometry, waterLevel);

    private static CrossSectionHydraulicResult Calculate(
        CrossSectionHydraulicResultCalculator calculator,
        CrossSectionGeometry geometry,
        double waterLevel) =>
        calculator.Calculate(
            geometry,
            waterLevel,
            Gravity,
            KinematicViscosity,
            Depth,
            Width,
            HydraulicSlope);

    private static void AssertResultIsValid(CrossSectionHydraulicResult result)
    {
        AssertFinite(
            result.WaterLevel,
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
        Assert.Equal(HydraulicSlope, result.HydraulicSlope);
    }

    private static void AssertResultsEqual(
        CrossSectionHydraulicResult expected,
        CrossSectionHydraulicResult actual)
    {
        AssertClose(expected.WaterLevel, actual.WaterLevel);
        AssertClose(expected.Omega, actual.Omega);
        AssertClose(expected.Chi, actual.Chi);
        AssertClose(expected.HydraulicRadius, actual.HydraulicRadius);
        AssertClose(expected.HydraulicSlope, actual.HydraulicSlope);
        AssertClose(expected.ShearVelocity, actual.ShearVelocity);
        AssertClose(expected.ChezyCoefficient, actual.ChezyCoefficient);
        AssertClose(expected.Velocity, actual.Velocity);
        AssertClose(expected.Discharge, actual.Discharge);
    }

    private static void AssertFinite(params double[] values)
    {
        Assert.All(values, value =>
        {
            Assert.False(double.IsNaN(value));
            Assert.False(double.IsInfinity(value));
        });
    }

    private static void AssertClose(double expected, double actual)
    {
        Assert.True(
            Math.Abs(expected - actual) <= 1e-12,
            $"Expected {expected:R}, actual {actual:R}.");
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
}
