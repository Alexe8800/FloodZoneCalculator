using System.Globalization;
using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class MultiCrossSectionHydraulicResultTests
{
    private const double Gravity = 9.81;
    private const double KinematicViscosity = 1.15e-6;
    private const double Depth = 5;
    private const double Width = 50;
    private const double HydraulicSlope = 0.001;

    [Fact]
    public void Calculates_five_sections_in_longitudinal_order_and_matches_individual_results()
    {
        var sections = CreateSectionsInRandomOrder();
        var inputOrder = sections.Select(section => section.Number).ToArray();
        var snapshots = sections.ToDictionary(section => section.Number, Snapshot);
        var summary = CalculateSummary(sections, 106);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, summary.Sections.Select(item => item.Number));
        Assert.Equal(new[] { 0d, 1000d, 2500d, 4000d, 7000d },
            summary.Sections.Select(item => item.DistanceFromHydroUnitM));
        Assert.Equal(inputOrder, sections.Select(section => section.Number));

        foreach (var item in summary.Sections)
        {
            var section = sections.Single(section => section.Number == item.Number);
            var geometry = new CrossSectionGeometryBuilder().Build(section);
            var individual = Calculate(geometry, 106);

            AssertResultsEqual(individual, item.HydraulicResult);
            Assert.Equal(snapshots[item.Number], Snapshot(section));
            AssertResultIsValid(item.HydraulicResult);
        }

        var incomplete = sections.Single(section => section.Number == 3);
        var incompleteGeometry = new CrossSectionGeometryBuilder().Build(incomplete);
        Assert.Equal(5, incompleteGeometry.Points.Count);
        Assert.Equal(4, incompleteGeometry.Segments.Count);
    }

    [Fact]
    public void Supports_a_single_section()
    {
        var section = Configure(TestCrossSectionFactory.Symmetric(), 1, 0);

        var summary = CalculateSummary(new[] { section }, 106);

        var item = Assert.Single(summary.Sections);
        Assert.Equal(1, item.Number);
        Assert.Equal(0, item.DistanceFromHydroUnitM);
        AssertResultIsValid(item.HydraulicResult);
    }

    [Fact]
    public void Rejects_empty_sequence()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CalculateSummary(Array.Empty<CrossSectionRecord>(), 106));

        Assert.Contains("пустой", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_null_section()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            CalculateSummary(new CrossSectionRecord[] { null! }, 106));

        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_duplicate_distance()
    {
        var sections = new[]
        {
            Configure(TestCrossSectionFactory.Asymmetric(), 2, 1000),
            Configure(TestCrossSectionFactory.Incomplete(), 3, 1000)
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            CalculateSummary(sections, 106));

        Assert.Contains("дубликат", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_negative_distance()
    {
        var section = Configure(TestCrossSectionFactory.Symmetric(), 1, -1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CalculateSummary(new[] { section }, 106));

        Assert.Equal("DistanceFromHydroUnitM", exception.ParamName);
    }

    [Fact]
    public void Rejects_invalid_hydraulic_input_through_existing_calculator()
    {
        var section = Configure(TestCrossSectionFactory.Symmetric(), 1, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new MultiCrossSectionHydraulicResultCalculator().Calculate(
                new[] { section },
                106,
                gravity: 0,
                kinematicViscosity: KinematicViscosity,
                depth: Depth,
                width: Width,
                hydraulicSlope: HydraulicSlope));
    }

    [Fact]
    public void Is_repeatable_and_does_not_mutate_input_sections()
    {
        var sections = CreateSectionsInRandomOrder();
        var snapshots = sections.ToDictionary(section => section.Number, Snapshot);
        var calculator = new MultiCrossSectionHydraulicResultCalculator();

        var first = calculator.Calculate(
            sections, 106, Gravity, KinematicViscosity, Depth, Width, HydraulicSlope);
        var second = calculator.Calculate(
            sections, 106, Gravity, KinematicViscosity, Depth, Width, HydraulicSlope);

        AssertSummariesEqual(first, second);
        Assert.Equal(new[] { 4, 1, 5, 3, 2 }, sections.Select(section => section.Number));
        foreach (var section in sections)
            Assert.Equal(snapshots[section.Number], Snapshot(section));
    }

    [Fact]
    public void Different_water_levels_are_independent_and_do_not_mutate_geometry()
    {
        var sections = CreateSectionsInRandomOrder();
        var sourceSnapshots = sections.ToDictionary(section => section.Number, Snapshot);
        var geometrySnapshots = sections.ToDictionary(
            section => section.Number,
            section => Snapshot(new CrossSectionGeometryBuilder().Build(section)));
        var calculator = new MultiCrossSectionHydraulicResultCalculator();

        var at106 = calculator.Calculate(
            sections, 106, Gravity, KinematicViscosity, Depth, Width, HydraulicSlope);
        var at105 = calculator.Calculate(
            sections, 105, Gravity, KinematicViscosity, Depth, Width, HydraulicSlope);

        Assert.All(at106.Sections, item => Assert.Equal(106, item.HydraulicResult.WaterLevel));
        Assert.All(at105.Sections, item => Assert.Equal(105, item.HydraulicResult.WaterLevel));

        var first106 = at106.Sections.Single(item => item.Number == 1).HydraulicResult;
        var first105 = at105.Sections.Single(item => item.Number == 1).HydraulicResult;
        Assert.NotEqual(first106.Omega, first105.Omega);
        Assert.NotEqual(first106.Chi, first105.Chi);

        foreach (var section in sections)
        {
            Assert.Equal(sourceSnapshots[section.Number], Snapshot(section));
            Assert.Equal(
                geometrySnapshots[section.Number],
                Snapshot(new CrossSectionGeometryBuilder().Build(section)));
        }
    }

    private static MultiCrossSectionHydraulicResult CalculateSummary(
        IEnumerable<CrossSectionRecord> sections,
        double waterLevel) =>
        new MultiCrossSectionHydraulicResultCalculator().Calculate(
            sections,
            waterLevel,
            Gravity,
            KinematicViscosity,
            Depth,
            Width,
            HydraulicSlope);

    private static CrossSectionHydraulicResult Calculate(
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

    private static CrossSectionRecord[] CreateSectionsInRandomOrder() =>
        new[]
        {
            Configure(TestCrossSectionFactory.FlatBottom(), 4, 4000),
            Configure(TestCrossSectionFactory.Symmetric(), 1, 0),
            Configure(TestCrossSectionFactory.SlopedBottom(), 5, 7000),
            Configure(TestCrossSectionFactory.Incomplete(), 3, 2500),
            Configure(TestCrossSectionFactory.Asymmetric(), 2, 1000)
        };

    private static CrossSectionRecord Configure(
        CrossSectionRecord section,
        int number,
        double distance)
    {
        section.Number = number;
        section.DistanceFromHydroUnitM = distance.ToString(CultureInfo.InvariantCulture);
        return section;
    }

    private static void AssertSummariesEqual(
        MultiCrossSectionHydraulicResult expected,
        MultiCrossSectionHydraulicResult actual)
    {
        Assert.Equal(expected.Sections.Count, actual.Sections.Count);
        for (var index = 0; index < expected.Sections.Count; index++)
        {
            var expectedItem = expected.Sections[index];
            var actualItem = actual.Sections[index];
            Assert.Equal(expectedItem.Number, actualItem.Number);
            AssertClose(
                expectedItem.DistanceFromHydroUnitM,
                actualItem.DistanceFromHydroUnitM);
            AssertResultsEqual(expectedItem.HydraulicResult, actualItem.HydraulicResult);
        }
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
            .Prepend(
                $"{section.Number}|{section.DistanceFromHydroUnitM}|{section.BottomLevelZb}|{section.DepthHb}|{section.WidthBb}|{section.VelocityVb}|{section.Kgm}")
            .ToArray();

    private static string[] Snapshot(CrossSectionGeometry geometry) =>
        geometry.Points
            .Select(point =>
                $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM:R}|{point.ElevationM:R}")
            .Concat(geometry.Segments.Select(segment =>
                $"{segment.Start.PointNumber}->{segment.End.PointNumber}"))
            .ToArray();
}
