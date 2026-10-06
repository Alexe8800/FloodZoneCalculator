using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class AdjacentCrossSectionPairTests
{
    [Fact]
    public void Calculates_distance_delta_from_sequence_order()
    {
        var pairs = BuildPairs(CreateSequence());

        Assert.Equal(4, pairs.Count);
        AssertPair(pairs[0], 1, 2, 0, 1000, 1000);
        AssertPair(pairs[1], 2, 3, 1000, 2500, 1500);
        AssertPair(pairs[2], 3, 4, 2500, 4000, 1500);
        AssertPair(pairs[3], 4, 5, 4000, 7000, 3000);
    }

    [Fact]
    public void Uses_spatial_order_for_random_input()
    {
        var sections = CreateSectionsInInputOrder();
        var sequence = new CrossSectionSequence(sections);

        var pairs = BuildPairs(sequence);

        Assert.Equal(
            new[] { "1->2", "2->3", "3->4", "4->5" },
            pairs.Select(pair => $"{pair.First.Number}->{pair.Second.Number}"));
    }

    [Fact]
    public void Does_not_use_number_as_adjacency_criterion()
    {
        var sections = CreateSections(
            new[] { 30, 10, 50, 20, 40 },
            new[] { 0d, 1000d, 2500d, 4000d, 7000d });
        var sequence = new CrossSectionSequence(sections);

        var pairs = BuildPairs(sequence);

        Assert.Equal(
            new[] { "30->10", "10->50", "50->20", "20->40" },
            pairs.Select(pair => $"{pair.First.Number}->{pair.Second.Number}"));
    }

    [Fact]
    public void One_section_produces_zero_pairs()
    {
        var section = Configure(TestCrossSectionFactory.Symmetric(), 1, 0);

        var pairs = BuildPairs(new CrossSectionSequence(new[] { section }));

        Assert.Empty(pairs);
    }

    [Fact]
    public void Two_sections_produce_one_pair()
    {
        var sequence = new CrossSectionSequence(CreateSections(
            new[] { 1, 2 },
            new[] { 0d, 1000d }));

        var pairs = BuildPairs(sequence);

        var pair = Assert.Single(pairs);
        AssertPair(pair, 1, 2, 0, 1000, 1000);
    }

    [Fact]
    public void Rejects_null_sequence()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CrossSectionAdjacentPairBuilder().Build(null!));
    }

    [Fact]
    public void Rejects_null_pair_elements()
    {
        var item = new CrossSectionSequenceItem(1, 0);

        Assert.Throws<ArgumentNullException>(() =>
            new AdjacentCrossSectionPair(null!, item));
        Assert.Throws<ArgumentNullException>(() =>
            new AdjacentCrossSectionPair(item, null!));
    }

    [Fact]
    public void Rejects_pair_in_reverse_spatial_order()
    {
        var first = new CrossSectionSequenceItem(1, 1000);
        var second = new CrossSectionSequenceItem(2, 0);

        Assert.Throws<ArgumentException>(() =>
            new AdjacentCrossSectionPair(first, second));
    }

    [Fact]
    public void Repeated_builds_are_identical_and_do_not_mutate_sequence()
    {
        var sequence = CreateSequence();
        var numbersBefore = sequence.Items.Select(item => item.Number).ToArray();
        var distancesBefore = sequence.Items
            .Select(item => item.DistanceFromHydroUnitM)
            .ToArray();
        var builder = new CrossSectionAdjacentPairBuilder();

        var first = builder.Build(sequence);
        var second = builder.Build(sequence);

        Assert.Equal(
            first.Select(pair => $"{pair.First.Number}->{pair.Second.Number}"),
            second.Select(pair => $"{pair.First.Number}->{pair.Second.Number}"));
        Assert.Equal(
            first.Select(pair => pair.DistanceDeltaM),
            second.Select(pair => pair.DistanceDeltaM));
        Assert.Equal(numbersBefore, sequence.Items.Select(item => item.Number));
        Assert.Equal(distancesBefore, sequence.Items.Select(item => item.DistanceFromHydroUnitM));
    }

    [Fact]
    public void Moving_profile_02_changes_only_spatial_pairing_and_deltas()
    {
        var sections = CreateSections(
            new[] { 1, 2, 3, 4, 5 },
            new[] { 0d, 6000d, 2500d, 4000d, 7000d });
        var sequence = new CrossSectionSequence(sections);

        var pairs = BuildPairs(sequence);

        Assert.Equal(
            new[] { "1->3", "3->4", "4->2", "2->5" },
            pairs.Select(pair => $"{pair.First.Number}->{pair.Second.Number}"));
        Assert.Equal(new[] { 2500d, 1500d, 2000d, 1000d },
            pairs.Select(pair => pair.DistanceDeltaM));
    }

    [Fact]
    public void Builder_does_not_change_source_records_or_geometry()
    {
        var sections = CreateSectionsInInputOrder();
        var recordSnapshots = sections.ToDictionary(section => section.Number, Snapshot);
        var geometries = sections.ToDictionary(
            section => section.Number,
            section => new CrossSectionGeometryBuilder().Build(section));
        var geometrySnapshots = geometries.ToDictionary(
            pair => pair.Key,
            pair => GeometrySnapshot(pair.Value));
        var sequence = new CrossSectionSequence(sections);

        _ = BuildPairs(sequence);

        foreach (var section in sections)
        {
            Assert.Equal(recordSnapshots[section.Number], Snapshot(section));
            Assert.Equal(
                geometrySnapshots[section.Number],
                GeometrySnapshot(geometries[section.Number]));
        }
    }

    [Fact]
    public void Sequence_and_pairs_remain_separate_from_hydraulic_calculation()
    {
        var sections = CreateSections(
            new[] { 1, 2, 3, 4, 5 },
            new[] { 0d, 1000d, 2500d, 4000d, 7000d });
        var sequence = new CrossSectionSequence(sections);
        var pairs = BuildPairs(sequence);

        Assert.All(pairs, pair =>
        {
            Assert.NotNull(pair.First);
            Assert.NotNull(pair.Second);
            Assert.True(pair.DistanceDeltaM >= 0);
        });
    }

    private static IReadOnlyList<AdjacentCrossSectionPair> BuildPairs(
        CrossSectionSequence sequence) =>
        new CrossSectionAdjacentPairBuilder().Build(sequence);

    private static CrossSectionSequence CreateSequence() =>
        new CrossSectionSequence(CreateSections(
            new[] { 4, 1, 5, 3, 2 },
            new[] { 4000d, 0d, 7000d, 2500d, 1000d }));

    private static CrossSectionRecord[] CreateSectionsInInputOrder() =>
        CreateSections(
            new[] { 4, 1, 5, 3, 2 },
            new[] { 4000d, 0d, 7000d, 2500d, 1000d });

    private static CrossSectionRecord[] CreateSections(
        int[] numbers,
        double[] distances)
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
            .Select((profile, index) => Configure(
                profile,
                numbers[index],
                distances[index]))
            .ToArray();
    }

    private static CrossSectionRecord Configure(
        CrossSectionRecord section,
        int number,
        double distance)
    {
        section.Number = number;
        section.DistanceFromHydroUnitM =
            distance.ToString(CultureInfo.InvariantCulture);
        return section;
    }

    private static void AssertPair(
        AdjacentCrossSectionPair pair,
        int firstNumber,
        int secondNumber,
        double firstDistance,
        double secondDistance,
        double delta)
    {
        Assert.Equal(firstNumber, pair.First.Number);
        Assert.Equal(secondNumber, pair.Second.Number);
        Assert.Equal(firstDistance, pair.First.DistanceFromHydroUnitM);
        Assert.Equal(secondDistance, pair.Second.DistanceFromHydroUnitM);
        Assert.Equal(delta, pair.DistanceDeltaM);
        Assert.True(pair.DistanceDeltaM >= 0);
    }

    private static string[] Snapshot(CrossSectionRecord section) =>
        section.BankPoints
            .Select(point =>
                $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}")
            .Prepend($"{section.Number}|{section.DistanceFromHydroUnitM}")
            .ToArray();

    private static string[] GeometrySnapshot(CrossSectionGeometry geometry) =>
        geometry.Points
            .Select(point => $"{point.DistanceM:R}|{point.ElevationM:R}")
            .Concat(geometry.Segments.Select(segment =>
                $"{segment.Start.DistanceM:R},{segment.Start.ElevationM:R}" +
                $"->{segment.End.DistanceM:R},{segment.End.ElevationM:R}"))
            .ToArray();
}
