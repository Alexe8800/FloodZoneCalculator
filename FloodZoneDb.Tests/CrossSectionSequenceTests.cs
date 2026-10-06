using System;
using System.Globalization;
using System.Linq;
using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class CrossSectionSequenceTests
{
    [Fact]
    public void Rejects_empty_sequence()
    {
        Assert.Throws<ArgumentException>(() =>
            new CrossSectionSequence(Array.Empty<CrossSectionRecord>()));
    }

    [Fact]
    public void Rejects_null_sequence()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CrossSectionSequence(null!));
    }

    [Fact]
    public void Rejects_null_element()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new CrossSectionSequence(new CrossSectionRecord[] { null! }));

        Assert.Equal("sections", exception.ParamName);
    }

    [Fact]
    public void Supports_one_section_at_zero_distance()
    {
        var section = Configure(TestCrossSectionFactory.Symmetric(), 1, 0);

        var sequence = new CrossSectionSequence(new[] { section });

        var item = Assert.Single(sequence.Items);
        Assert.Equal(1, item.Number);
        Assert.Equal(0, item.DistanceFromHydroUnitM);
    }

    [Fact]
    public void Sorts_five_sections_by_distance_without_mutating_input_order()
    {
        var sections = new[]
        {
            Configure(TestCrossSectionFactory.FlatBottom(), 4, 4000),
            Configure(TestCrossSectionFactory.Symmetric(), 1, 0),
            Configure(TestCrossSectionFactory.SlopedBottom(), 5, 7000),
            Configure(TestCrossSectionFactory.Incomplete(), 3, 2500),
            Configure(TestCrossSectionFactory.Asymmetric(), 2, 1000)
        };
        var inputOrder = sections.Select(section => section.Number).ToArray();

        var sequence = new CrossSectionSequence(sections);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, sequence.Items.Select(item => item.Number));
        Assert.Equal(new[] { 0d, 1000d, 2500d, 4000d, 7000d },
            sequence.Items.Select(item => item.DistanceFromHydroUnitM));
        Assert.Equal(inputOrder, sections.Select(section => section.Number));
    }

    [Fact]
    public void Rejects_duplicate_distance()
    {
        var sections = new[]
        {
            Configure(TestCrossSectionFactory.Symmetric(), 1, 1000),
            Configure(TestCrossSectionFactory.Asymmetric(), 2, 1000)
        };

        Assert.Throws<ArgumentException>(() => new CrossSectionSequence(sections));
    }

    [Fact]
    public void Rejects_negative_distance()
    {
        var section = Configure(TestCrossSectionFactory.Symmetric(), 1, -1);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CrossSectionSequence(new[] { section }));

        Assert.Equal("value", exception.ParamName);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void Rejects_non_finite_distance(string distance)
    {
        var section = Configure(TestCrossSectionFactory.Symmetric(), 1, distance);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CrossSectionSequence(new[] { section }));
    }

    [Fact]
    public void Recreating_sequence_from_same_data_is_stable_and_read_only()
    {
        var sections = CreateSections();

        var first = new CrossSectionSequence(sections);
        var second = new CrossSectionSequence(sections);

        Assert.Equal(
            first.Items.Select(item => item.Number),
            second.Items.Select(item => item.Number));
        Assert.Equal(
            first.Items.Select(item => item.DistanceFromHydroUnitM),
            second.Items.Select(item => item.DistanceFromHydroUnitM));
        Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyList<CrossSectionSequenceItem>>(
            first.Items);
        Assert.Equal(new[] { 4, 1, 5, 3, 2 }, sections.Select(section => section.Number));
    }

    [Fact]
    public void Changing_profile_02_distance_reorders_metadata_without_changing_profile_identity()
    {
        var sections = CreateSections();
        sections.Single(section => section.Number == 2).DistanceFromHydroUnitM = "6000";

        var sequence = new CrossSectionSequence(sections);

        Assert.Equal(new[] { 1, 3, 4, 2, 5 }, sequence.Items.Select(item => item.Number));
        Assert.Equal(2, sequence.Items[3].Number);
        Assert.Equal(6000, sequence.Items[3].DistanceFromHydroUnitM);
    }

    [Fact]
    public void Profile_03_remains_five_points_and_four_segments()
    {
        var section = Configure(TestCrossSectionFactory.Incomplete(), 3, 2500);
        var geometry = Build(section);

        _ = new CrossSectionSequence(new[] { section });

        Assert.Equal(5, geometry.Points.Count);
        Assert.Equal(4, geometry.Segments.Count);
    }

    [Fact]
    public void Different_distances_change_sequence_metadata_but_not_independent_hydraulic_results()
    {
        var setA = CreateSections(new[] { 0d, 1000d, 2500d, 4000d, 7000d });
        var setB = CreateSections(new[] { 0d, 500d, 3000d, 8000d, 15000d });
        var input = new HydraulicCalculationInput(106, 9.81, 1.15e-6, 5, 50, 0.001);
        var calculator = new CrossSectionHydraulicResultCalculator();

        var sequenceA = new CrossSectionSequence(setA);
        var sequenceB = new CrossSectionSequence(setB);

        foreach (var number in Enumerable.Range(1, 5))
        {
            var resultA = SingleResult(setA.Single(section => section.Number == number), input, calculator);
            var resultB = SingleResult(setB.Single(section => section.Number == number), input, calculator);
            AssertResultsEqual(resultA, resultB);
        }

        Assert.NotEqual(
            sequenceA.Items.Select(item => item.DistanceFromHydroUnitM).ToArray(),
            sequenceB.Items.Select(item => item.DistanceFromHydroUnitM).ToArray());
    }

    [Fact]
    public void Sequence_does_not_mutate_records_bank_points_or_geometry()
    {
        var sections = CreateSections();
        var recordSnapshots = sections.ToDictionary(section => section.Number, Snapshot);
        var geometries = sections.ToDictionary(section => section.Number, Build);
        var geometrySnapshots = geometries.ToDictionary(
            pair => pair.Key,
            pair => Snapshot(pair.Value));

        _ = new CrossSectionSequence(sections);

        foreach (var section in sections)
        {
            Assert.Equal(recordSnapshots[section.Number], Snapshot(section));
            Assert.Equal(geometrySnapshots[section.Number], Snapshot(geometries[section.Number]));
        }
    }

    private static CrossSectionRecord[] CreateSections(
        double[]? distances = null)
    {
        distances ??= new[] { 0d, 1000d, 2500d, 4000d, 7000d };
        return new[]
        {
            Configure(TestCrossSectionFactory.FlatBottom(), 4, distances[3]),
            Configure(TestCrossSectionFactory.Symmetric(), 1, distances[0]),
            Configure(TestCrossSectionFactory.SlopedBottom(), 5, distances[4]),
            Configure(TestCrossSectionFactory.Incomplete(), 3, distances[2]),
            Configure(TestCrossSectionFactory.Asymmetric(), 2, distances[1])
        };
    }

    private static CrossSectionRecord Configure(
        CrossSectionRecord section,
        int number,
        double distance)
    {
        section.Number = number;
        return Configure(section, number, distance.ToString(CultureInfo.InvariantCulture));
    }

    private static CrossSectionRecord Configure(
        CrossSectionRecord section,
        int number,
        string distance)
    {
        section.Number = number;
        section.DistanceFromHydroUnitM = distance;
        return section;
    }

    private static CrossSectionGeometry Build(CrossSectionRecord section) =>
        new CrossSectionGeometryBuilder().Build(section);

    private static CrossSectionHydraulicResult SingleResult(
        CrossSectionRecord section,
        HydraulicCalculationInput input,
        CrossSectionHydraulicResultCalculator calculator) =>
        calculator.Calculate(new CrossSectionHydraulicContext(Build(section), input));

    private static string[] Snapshot(CrossSectionRecord section) =>
        section.BankPoints
            .Select(point =>
                $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}")
            .Prepend(
                $"{section.Number}|{section.DistanceFromHydroUnitM}")
            .ToArray();

    private static string[] Snapshot(CrossSectionGeometry geometry) =>
        geometry.Points
            .Select(point => $"{point.DistanceM:R}|{point.ElevationM:R}")
            .Concat(geometry.Segments.Select(segment =>
                $"{segment.Start.DistanceM:R},{segment.Start.ElevationM:R}" +
                $"->{segment.End.DistanceM:R},{segment.End.ElevationM:R}"))
            .ToArray();

    private static void AssertResultsEqual(
        CrossSectionHydraulicResult expected,
        CrossSectionHydraulicResult actual)
    {
        Assert.Equal(expected.WaterLevel, actual.WaterLevel, 12);
        Assert.Equal(expected.Omega, actual.Omega, 12);
        Assert.Equal(expected.Chi, actual.Chi, 12);
        Assert.Equal(expected.HydraulicRadius, actual.HydraulicRadius, 12);
        Assert.Equal(expected.HydraulicSlope, actual.HydraulicSlope, 12);
        Assert.Equal(expected.ShearVelocity, actual.ShearVelocity, 12);
        Assert.Equal(expected.ChezyCoefficient, actual.ChezyCoefficient, 12);
        Assert.Equal(expected.Velocity, actual.Velocity, 12);
        Assert.Equal(expected.Discharge, actual.Discharge, 12);
    }
}
