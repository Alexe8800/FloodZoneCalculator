using System.Globalization;
using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class CrossSectionLongitudinalSequenceTests
{
    private const double Gravity = 9.81;
    private const double KinematicViscosity = 1.15e-6;
    private const double Depth = 5;
    private const double Width = 50;
    private const double HydraulicSlope = 0.001;

    [Fact]
    public void Sorts_five_sections_by_longitudinal_distance_without_mutating_input()
    {
        var input = CreateSectionsInRandomOrder();
        var inputOrder = input.Select(section => section.Number).ToArray();

        var ordered = OrderSequence(input);

        Assert.Equal(new[] { 4, 1, 5, 3, 2 }, inputOrder);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, ordered.Select(section => section.Number));
        Assert.Equal(
            new[] { 0d, 1000d, 2500d, 4000d, 7000d },
            ordered.Select(GetDistance));
        Assert.Equal(inputOrder, input.Select(section => section.Number));
    }

    [Fact]
    public void Supports_a_single_section_at_zero_distance()
    {
        var section = Configure(TestCrossSectionFactory.Symmetric(), 1, 0);

        var ordered = OrderSequence(new[] { section });

        var only = Assert.Single(ordered);
        Assert.Equal(1, only.Number);
        Assert.Equal(0, GetDistance(only));
    }

    [Fact]
    public void Rejects_duplicate_longitudinal_distances()
    {
        var sections = new[]
        {
            Configure(TestCrossSectionFactory.Asymmetric(), 2, 1000),
            Configure(TestCrossSectionFactory.Incomplete(), 3, 1000)
        };

        var exception = Assert.Throws<ArgumentException>(() => OrderSequence(sections));

        Assert.Contains("дубликат", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_negative_longitudinal_distance()
    {
        var section = Configure(TestCrossSectionFactory.Symmetric(), 1, -1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            OrderSequence(new[] { section }));

        Assert.Equal("DistanceFromHydroUnitM", exception.ParamName);
    }

    [Fact]
    public void Preserves_section_data_and_hydraulic_results_independently()
    {
        var sections = CreateSectionsInRandomOrder();
        var snapshots = sections.ToDictionary(section => section.Number, Snapshot);
        var results = new Dictionary<int, CrossSectionHydraulicResult>();
        var geometrySnapshots = new Dictionary<int, string[]>();

        foreach (var section in OrderSequence(sections))
        {
            var geometry = new CrossSectionGeometryBuilder().Build(section);
            geometrySnapshots[section.Number] = Snapshot(geometry);
            results[section.Number] = Calculate(geometry, 106);

            AssertResultIsValid(results[section.Number]);
            Assert.Equal(snapshots[section.Number], Snapshot(section));
        }

        foreach (var section in sections)
        {
            var geometry = new CrossSectionGeometryBuilder().Build(section);
            var recalculated = Calculate(geometry, 106);

            Assert.Equal(snapshots[section.Number], Snapshot(section));
            Assert.Equal(geometrySnapshots[section.Number], Snapshot(geometry));
            AssertResultsEqual(results[section.Number], recalculated);
        }

        Assert.Equal(5, geometrySnapshots[3].Count(value => value.Contains("->")) + 1);
    }

    [Fact]
    public void Incomplete_profile_stays_incomplete_at_its_longitudinal_position()
    {
        var section = Configure(TestCrossSectionFactory.Incomplete(), 3, 2500);
        var geometry = new CrossSectionGeometryBuilder().Build(section);
        var before = Snapshot(geometry);

        _ = Calculate(geometry, 106);

        Assert.Equal(2500, GetDistance(section));
        Assert.Equal(5, geometry.Points.Count);
        Assert.Equal(4, geometry.Segments.Count);
        Assert.Equal(before, Snapshot(geometry));
    }

    [Fact]
    public void Longitudinal_distance_is_not_used_by_hydraulic_formulas()
    {
        var first = Configure(TestCrossSectionFactory.Symmetric(), 1, 0);
        var moved = Configure(TestCrossSectionFactory.Symmetric(), 1, 7000);
        var firstGeometry = new CrossSectionGeometryBuilder().Build(first);
        var movedGeometry = new CrossSectionGeometryBuilder().Build(moved);

        var firstResult = Calculate(firstGeometry, 106);
        var movedResult = Calculate(movedGeometry, 106);

        AssertResultsEqual(firstResult, movedResult);
    }

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
        section.DistanceFromHydroUnitM = distance.ToString(
            CultureInfo.InvariantCulture);
        return section;
    }

    private static IReadOnlyList<CrossSectionRecord> OrderSequence(
        IEnumerable<CrossSectionRecord> sections)
    {
        var parsed = sections
            .Select(section => (Section: section, Distance: GetDistance(section)))
            .ToArray();

        var duplicate = parsed
            .GroupBy(item => item.Distance)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            throw new ArgumentException(
                "Продольная последовательность содержит дубликат DistanceFromHydroUnitM.",
                nameof(sections));

        return parsed
            .OrderBy(item => item.Distance)
            .Select(item => item.Section)
            .ToArray();
    }

    private static double GetDistance(CrossSectionRecord section)
    {
        if (!double.TryParse(
                section.DistanceFromHydroUnitM,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var distance) ||
            double.IsNaN(distance) ||
            double.IsInfinity(distance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(section.DistanceFromHydroUnitM),
                section.DistanceFromHydroUnitM,
                "DistanceFromHydroUnitM должен быть конечным числом.");
        }

        if (distance < 0)
            throw new ArgumentOutOfRangeException(
                nameof(section.DistanceFromHydroUnitM),
                section.DistanceFromHydroUnitM,
                "DistanceFromHydroUnitM не может быть отрицательным.");

        return distance;
    }

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
