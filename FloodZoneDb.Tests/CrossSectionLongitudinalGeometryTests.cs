using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class CrossSectionLongitudinalGeometryTests
{
    [Fact]
    public void Preserves_negative_bottom_level_difference()
    {
        var geometry = BuildGeometry(1, 0, 65, 2, 1000, 64);

        Assert.Equal(1000, geometry.DeltaDistanceM);
        Assert.Equal(65, geometry.FirstBottomLevelZb);
        Assert.Equal(64, geometry.SecondBottomLevelZb);
        Assert.Equal(-1, geometry.BottomLevelDifferenceM);
    }

    [Fact]
    public void Preserves_positive_bottom_level_difference()
    {
        var geometry = BuildGeometry(1, 0, 64, 2, 1000, 65);

        Assert.Equal(1, geometry.BottomLevelDifferenceM);
    }

    [Fact]
    public void Preserves_zero_bottom_level_difference()
    {
        var geometry = BuildGeometry(1, 0, 65, 2, 1000, 65);

        Assert.Equal(0, geometry.BottomLevelDifferenceM);
    }

    [Fact]
    public void Uses_pair_first_and_second_even_when_numbers_are_not_sequential()
    {
        var geometry = BuildGeometry(30, 0, 65, 10, 1000, 64);

        Assert.Equal(30, geometry.Pair.First.Number);
        Assert.Equal(10, geometry.Pair.Second.Number);
        Assert.Equal(-1, geometry.BottomLevelDifferenceM);
    }

    [Fact]
    public void Builds_four_geometric_sections_for_five_profiles_in_spatial_order()
    {
        var sections = CreateSections(
            new[] { 4, 1, 5, 3, 2 },
            new[] { 4000d, 0d, 7000d, 2500d, 1000d },
            new[] { 67d, 65d, 63d, 66d, 64d });
        var sequence = new CrossSectionSequence(sections);
        var pairs = new CrossSectionAdjacentPairBuilder().Build(sequence);
        var records = sections.ToDictionary(section => section.Number);

        var geometries = pairs
            .Select(pair => new CrossSectionLongitudinalGeometryBuilder().Build(
                pair,
                records[pair.First.Number],
                records[pair.Second.Number]))
            .ToArray();

        Assert.Equal(4, geometries.Length);
        Assert.Equal(
            new[] { "1->2", "2->3", "3->4", "4->5" },
            geometries.Select(geometry =>
                $"{geometry.Pair.First.Number}->{geometry.Pair.Second.Number}"));
        Assert.Equal(new[] { 1000d, 1500d, 1500d, 3000d },
            geometries.Select(geometry => geometry.DeltaDistanceM));
    }

    [Fact]
    public void Moving_profile_02_rebuilds_geometry_in_new_spatial_order()
    {
        var sections = CreateSections(
            new[] { 1, 2, 3, 4, 5 },
            new[] { 0d, 6000d, 2500d, 4000d, 7000d },
            new[] { 65d, 64d, 66d, 67d, 63d });
        var sequence = new CrossSectionSequence(sections);
        var pairs = new CrossSectionAdjacentPairBuilder().Build(sequence);
        var records = sections.ToDictionary(section => section.Number);

        var geometries = pairs
            .Select(pair => new CrossSectionLongitudinalGeometryBuilder().Build(
                pair,
                records[pair.First.Number],
                records[pair.Second.Number]))
            .ToArray();

        Assert.Equal(
            new[] { "1->3", "3->4", "4->2", "2->5" },
            geometries.Select(geometry =>
                $"{geometry.Pair.First.Number}->{geometry.Pair.Second.Number}"));
        Assert.Equal(new[] { 1d, 1d, -3d, -1d },
            geometries.Select(geometry => geometry.BottomLevelDifferenceM));
    }

    [Fact]
    public void Copies_distance_delta_from_pair_without_recalculating_it()
    {
        var first = new CrossSectionSequenceItem(30, 0);
        var second = new CrossSectionSequenceItem(10, 1000);
        var pair = new AdjacentCrossSectionPair(first, second);
        var firstSection = Section(30, 65, 99999);
        var secondSection = Section(10, 64, -100);

        var geometry = new CrossSectionLongitudinalGeometryBuilder().Build(
            pair,
            firstSection,
            secondSection);

        Assert.Equal(pair.DistanceDeltaM, geometry.DeltaDistanceM);
        Assert.Equal(1000, geometry.DeltaDistanceM);
    }

    [Fact]
    public void Rejects_null_inputs_and_mismatched_records()
    {
        var pair = new AdjacentCrossSectionPair(
            new CrossSectionSequenceItem(1, 0),
            new CrossSectionSequenceItem(2, 1000));
        var builder = new CrossSectionLongitudinalGeometryBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.Build(null!, Section(1, 65, 0), Section(2, 1000, 1000)));
        Assert.Throws<ArgumentNullException>(() => builder.Build(pair, null!, Section(2, 64, 1000)));
        Assert.Throws<ArgumentNullException>(() => builder.Build(pair, Section(1, 65, 0), null!));
        Assert.Throws<ArgumentException>(() => builder.Build(pair, Section(9, 65, 0), Section(2, 64, 1000)));
        Assert.Throws<ArgumentException>(() => builder.Build(pair, Section(1, 65, 0), Section(9, 64, 1000)));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("not-a-number")]
    public void Rejects_invalid_bottom_level(string value)
    {
        var pair = new AdjacentCrossSectionPair(
            new CrossSectionSequenceItem(1, 0),
            new CrossSectionSequenceItem(2, 1000));
        var builder = new CrossSectionLongitudinalGeometryBuilder();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            builder.Build(pair, Section(1, value, 0), Section(2, "64", 1000)));
    }

    [Fact]
    public void Does_not_mutate_pair_sequence_records_or_geometry()
    {
        var sections = CreateSections(
            new[] { 4, 1, 5, 3, 2 },
            new[] { 4000d, 0d, 7000d, 2500d, 1000d },
            new[] { 67d, 65d, 63d, 66d, 64d });
        var recordSnapshots = sections.ToDictionary(section => section.Number, Snapshot);
        var sourceGeometry = sections.ToDictionary(
            section => section.Number,
            section => new CrossSectionGeometryBuilder().Build(section));
        var geometrySnapshots = sourceGeometry.ToDictionary(
            entry => entry.Key,
            entry => GeometrySnapshot(entry.Value));
        var sequence = new CrossSectionSequence(sections);
        var sequenceNumbers = sequence.Items.Select(item => item.Number).ToArray();
        var sequenceDistances = sequence.Items
            .Select(item => item.DistanceFromHydroUnitM)
            .ToArray();
        var pairs = new CrossSectionAdjacentPairBuilder().Build(sequence);
        var records = sections.ToDictionary(section => section.Number);

        _ = pairs.Select(pair => new CrossSectionLongitudinalGeometryBuilder().Build(
            pair,
            records[pair.First.Number],
            records[pair.Second.Number])).ToArray();

        Assert.Equal(sequenceNumbers, sequence.Items.Select(item => item.Number));
        Assert.Equal(sequenceDistances, sequence.Items.Select(item => item.DistanceFromHydroUnitM));
        foreach (var section in sections)
        {
            Assert.Equal(recordSnapshots[section.Number], Snapshot(section));
            Assert.Equal(
                geometrySnapshots[section.Number],
                GeometrySnapshot(sourceGeometry[section.Number]));
        }
    }

    [Fact]
    public void Repeated_builds_are_repeatable()
    {
        var first = BuildFiveGeometries();
        var second = BuildFiveGeometries();

        Assert.Equal(
            first.Select(geometry => geometry.BottomLevelDifferenceM),
            second.Select(geometry => geometry.BottomLevelDifferenceM));
        Assert.Equal(
            first.Select(geometry => geometry.DeltaDistanceM),
            second.Select(geometry => geometry.DeltaDistanceM));
    }

    private static CrossSectionLongitudinalGeometry BuildGeometry(
        int firstNumber,
        double firstDistance,
        double firstZb,
        int secondNumber,
        double secondDistance,
        double secondZb)
    {
        var pair = new AdjacentCrossSectionPair(
            new CrossSectionSequenceItem(firstNumber, firstDistance),
            new CrossSectionSequenceItem(secondNumber, secondDistance));

        return new CrossSectionLongitudinalGeometryBuilder().Build(
            pair,
            Section(firstNumber, firstZb, firstDistance),
            Section(secondNumber, secondZb, secondDistance));
    }

    private static CrossSectionLongitudinalGeometry[] BuildFiveGeometries()
    {
        var sections = CreateSections(
            new[] { 1, 2, 3, 4, 5 },
            new[] { 0d, 1000d, 2500d, 4000d, 7000d },
            new[] { 65d, 64d, 66d, 67d, 63d });
        var sequence = new CrossSectionSequence(sections);
        var pairs = new CrossSectionAdjacentPairBuilder().Build(sequence);
        var records = sections.ToDictionary(section => section.Number);

        return pairs.Select(pair =>
            new CrossSectionLongitudinalGeometryBuilder().Build(
                pair,
                records[pair.First.Number],
                records[pair.Second.Number])).ToArray();
    }

    private static CrossSectionRecord[] CreateSections(
        int[] numbers,
        double[] distances,
        double[] bottomLevels)
    {
        var profiles = new[]
        {
            TestCrossSectionFactory.FlatBottom(),
            TestCrossSectionFactory.Symmetric(),
            TestCrossSectionFactory.SlopedBottom(),
            TestCrossSectionFactory.Incomplete(),
            TestCrossSectionFactory.Asymmetric()
        };

        return profiles
            .Take(numbers.Length)
            .Select((profile, index) =>
            {
                profile.Number = numbers[index];
                profile.DistanceFromHydroUnitM =
                    distances[index].ToString(CultureInfo.InvariantCulture);
                profile.BottomLevelZb =
                    bottomLevels[index].ToString(CultureInfo.InvariantCulture);
                return profile;
            })
            .ToArray();
    }

    private static CrossSectionRecord Section(
        int number,
        double bottomLevel,
        double distance) =>
        Section(
            number,
            bottomLevel.ToString(CultureInfo.InvariantCulture),
            distance);

    private static CrossSectionRecord Section(
        int number,
        string bottomLevel,
        double distance) =>
        new()
        {
            Number = number,
            DistanceFromHydroUnitM =
                distance.ToString(CultureInfo.InvariantCulture),
            BottomLevelZb = bottomLevel
        };

    private static string[] Snapshot(CrossSectionRecord section) =>
        section.BankPoints
            .Select(point =>
                $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}")
            .Prepend(
                $"{section.Number}|{section.DistanceFromHydroUnitM}|{section.BottomLevelZb}")
            .ToArray();

    private static string[] GeometrySnapshot(CrossSectionGeometry geometry) =>
        geometry.Points
            .Select(point => $"{point.DistanceM:R}|{point.ElevationM:R}")
            .Concat(geometry.Segments.Select(segment =>
                $"{segment.Start.DistanceM:R},{segment.Start.ElevationM:R}" +
                $"->{segment.End.DistanceM:R},{segment.End.ElevationM:R}"))
            .ToArray();
}
