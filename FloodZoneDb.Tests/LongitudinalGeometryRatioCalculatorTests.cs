using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class LongitudinalGeometryRatioCalculatorTests
{
    private readonly LongitudinalGeometryRatioCalculator calculator = new();

    [Fact]
    public void Calculates_negative_ratio()
    {
        var geometry = CreateGeometry(1000, -1);

        Assert.Equal(-0.001, calculator.Calculate(geometry), 15);
    }

    [Fact]
    public void Calculates_positive_ratio()
    {
        var geometry = CreateGeometry(1000, 1);

        Assert.Equal(0.001, calculator.Calculate(geometry), 15);
    }

    [Fact]
    public void Calculates_zero_ratio()
    {
        var geometry = CreateGeometry(1000, 0);

        Assert.Equal(0, calculator.Calculate(geometry));
    }

    [Fact]
    public void Calculates_fractional_negative_ratio()
    {
        var geometry = CreateGeometry(1500, -2);

        Assert.Equal(-0.0013333333333333333, calculator.Calculate(geometry), 15);
    }

    [Fact]
    public void Direction_is_defined_by_spatial_sequence_order()
    {
        var sections = CreateSections(
            new[] { 0d, 1000d },
            new[] { 64d, 65d });
        var sequence = new CrossSectionSequence(sections.Reverse());
        var pair = new CrossSectionAdjacentPairBuilder().Build(sequence).Single();
        var geometry = new CrossSectionLongitudinalGeometryBuilder().Build(
            pair,
            sections.Single(section => section.Number == pair.First.Number),
            sections.Single(section => section.Number == pair.Second.Number));

        Assert.Equal(1, geometry.BottomLevelDifferenceM);
        Assert.Equal(0.001, calculator.Calculate(geometry), 15);
    }

    [Fact]
    public void Direction_preserves_negative_change_in_spatial_sequence()
    {
        var sections = CreateSections(
            new[] { 0d, 1000d },
            new[] { 65d, 64d });
        var sequence = new CrossSectionSequence(sections);
        var pair = new CrossSectionAdjacentPairBuilder().Build(sequence).Single();
        var geometry = new CrossSectionLongitudinalGeometryBuilder().Build(
            pair,
            sections[0],
            sections[1]);

        Assert.Equal(-1, geometry.BottomLevelDifferenceM);
        Assert.Equal(-0.001, calculator.Calculate(geometry), 15);
    }

    [Fact]
    public void Calculates_each_of_four_existing_section_geometries_independently()
    {
        var geometries = BuildFiveGeometries();

        Assert.Equal(4, geometries.Length);
        Assert.Equal(
            new[] { -0.001, 0.0013333333333333333, 0.0006666666666666666, -0.0013333333333333333 },
            geometries.Select(calculator.Calculate));
    }

    [Fact]
    public void Changing_profile_02_distance_changes_delta_and_ratio_only()
    {
        var original = BuildGeometries(
            new[] { 0d, 1000d, 2500d, 4000d, 7000d },
            new[] { 65d, 64d, 66d, 67d, 63d });
        var changed = BuildGeometries(
            new[] { 0d, 6000d, 2500d, 4000d, 7000d },
            new[] { 65d, 64d, 66d, 67d, 63d });

        var originalByPair = original.ToDictionary(
            geometry => $"{geometry.Pair.First.Number}->{geometry.Pair.Second.Number}");
        var changedByPair = changed.ToDictionary(
            geometry => $"{geometry.Pair.First.Number}->{geometry.Pair.Second.Number}");

        Assert.Equal(
            new[] { "1->2", "2->3", "3->4", "4->5" },
            original.Select(geometry =>
                $"{geometry.Pair.First.Number}->{geometry.Pair.Second.Number}"));
        Assert.Equal(
            new[] { "1->3", "3->4", "4->2", "2->5" },
            changed.Select(geometry =>
                $"{geometry.Pair.First.Number}->{geometry.Pair.Second.Number}"));

        Assert.Equal(65, changedByPair["1->3"].FirstBottomLevelZb);
        Assert.Equal(66, changedByPair["1->3"].SecondBottomLevelZb);
        Assert.Equal(1, changedByPair["1->3"].BottomLevelDifferenceM);
        Assert.Equal(2500, changedByPair["1->3"].DeltaDistanceM);
        Assert.Equal(0.0004, calculator.Calculate(changedByPair["1->3"]), 15);

        Assert.Equal(
            originalByPair["3->4"].BottomLevelDifferenceM,
            changedByPair["3->4"].BottomLevelDifferenceM);
        Assert.Equal(
            originalByPair["3->4"].DeltaDistanceM,
            changedByPair["3->4"].DeltaDistanceM);
    }

    [Fact]
    public void Changing_one_bottom_level_changes_only_adjacent_differences_and_ratios()
    {
        var original = BuildGeometries(
            new[] { 0d, 1000d, 2500d, 4000d, 7000d },
            new[] { 65d, 64d, 66d, 67d, 63d });
        var changed = BuildGeometries(
            new[] { 0d, 1000d, 2500d, 4000d, 7000d },
            new[] { 65d, 70d, 66d, 67d, 63d });

        var originalByPair = original.ToDictionary(
            geometry => $"{geometry.Pair.First.Number}->{geometry.Pair.Second.Number}");
        var changedByPair = changed.ToDictionary(
            geometry => $"{geometry.Pair.First.Number}->{geometry.Pair.Second.Number}");

        Assert.Equal(1000, changedByPair["1->2"].DeltaDistanceM);
        Assert.Equal(1500, changedByPair["2->3"].DeltaDistanceM);
        Assert.Equal(5, changedByPair["1->2"].BottomLevelDifferenceM);
        Assert.Equal(-4, changedByPair["2->3"].BottomLevelDifferenceM);
        Assert.NotEqual(
            calculator.Calculate(originalByPair["1->2"]),
            calculator.Calculate(changedByPair["1->2"]));
        Assert.NotEqual(
            calculator.Calculate(originalByPair["2->3"]),
            calculator.Calculate(changedByPair["2->3"]));

        foreach (var key in new[] { "3->4", "4->5" })
        {
            Assert.Equal(
                originalByPair[key].BottomLevelDifferenceM,
                changedByPair[key].BottomLevelDifferenceM);
            Assert.Equal(
                calculator.Calculate(originalByPair[key]),
                calculator.Calculate(changedByPair[key]),
                15);
        }
    }

    [Fact]
    public void Returns_finite_ratio_for_very_small_positive_distance()
    {
        var geometry = CreateGeometry(1e-300, 1);

        var ratio = calculator.Calculate(geometry);

        Assert.True(double.IsFinite(ratio));
        Assert.InRange(ratio, 9.999e299, 1.001e300);
    }

    [Fact]
    public void Rejects_null_geometry()
    {
        Assert.Throws<ArgumentNullException>(() => calculator.Calculate(null!));
    }

    [Fact]
    public void Rejects_zero_distance_without_fallback()
    {
        var geometry = CreateGeometry(0, 1);

        Assert.Throws<DivideByZeroException>(() => calculator.Calculate(geometry));
    }

    [Fact]
    public void Preserves_sign_and_does_not_mutate_geometry()
    {
        var geometry = CreateGeometry(1000, -1);
        var snapshot = new[]
        {
            geometry.DeltaDistanceM,
            geometry.FirstBottomLevelZb,
            geometry.SecondBottomLevelZb,
            geometry.BottomLevelDifferenceM
        };

        _ = calculator.Calculate(geometry);
        _ = calculator.Calculate(geometry);

        Assert.Equal(snapshot[0], geometry.DeltaDistanceM);
        Assert.Equal(snapshot[1], geometry.FirstBottomLevelZb);
        Assert.Equal(snapshot[2], geometry.SecondBottomLevelZb);
        Assert.Equal(snapshot[3], geometry.BottomLevelDifferenceM);
    }

    [Fact]
    public void Does_not_mutate_sequence_pair_records_or_source_geometry()
    {
        var sections = CreateSections(
            new[] { 0d, 1000d, 2500d, 4000d, 7000d },
            new[] { 65d, 64d, 66d, 67d, 63d });
        var recordSnapshots = sections.ToDictionary(section => section.Number, Snapshot);
        var sourceGeometry = sections.ToDictionary(
            section => section.Number,
            section => new CrossSectionGeometryBuilder().Build(section));
        var sourceGeometrySnapshots = sourceGeometry.ToDictionary(
            pair => pair.Key,
            pair => GeometrySnapshot(pair.Value));
        var sequence = new CrossSectionSequence(sections);
        var itemSnapshots = sequence.Items
            .Select(item => (item.Number, item.DistanceFromHydroUnitM))
            .ToArray();
        var pairs = new CrossSectionAdjacentPairBuilder().Build(sequence);
        var pairSnapshots = pairs
            .Select(pair => (
                pair.First.Number,
                pair.Second.Number,
                pair.DistanceDeltaM))
            .ToArray();
        var records = sections.ToDictionary(section => section.Number);

        foreach (var pair in pairs)
        {
            var geometry = new CrossSectionLongitudinalGeometryBuilder().Build(
                pair,
                records[pair.First.Number],
                records[pair.Second.Number]);
            _ = calculator.Calculate(geometry);
        }

        Assert.Equal(itemSnapshots, sequence.Items.Select(item =>
            (item.Number, item.DistanceFromHydroUnitM)));
        Assert.Equal(pairSnapshots, pairs.Select(pair =>
            (pair.First.Number, pair.Second.Number, pair.DistanceDeltaM)));
        foreach (var section in sections)
        {
            Assert.Equal(recordSnapshots[section.Number], Snapshot(section));
            Assert.Equal(
                sourceGeometrySnapshots[section.Number],
                GeometrySnapshot(sourceGeometry[section.Number]));
        }
    }

    [Fact]
    public void Repeated_calculation_is_repeatable()
    {
        var geometry = CreateGeometry(1500, -2);

        Assert.Equal(calculator.Calculate(geometry), calculator.Calculate(geometry), 15);
    }

    private static CrossSectionLongitudinalGeometry CreateGeometry(
        double deltaDistance,
        double bottomLevelDifference)
    {
        var pair = new AdjacentCrossSectionPair(
            new CrossSectionSequenceItem(1, 0),
            new CrossSectionSequenceItem(2, deltaDistance));

        return new CrossSectionLongitudinalGeometry(
            pair,
            65,
            65 + bottomLevelDifference);
    }

    private static CrossSectionLongitudinalGeometry[] BuildFiveGeometries()
    {
        return BuildGeometries(
            new[] { 0d, 1000d, 2500d, 4000d, 7000d },
            new[] { 65d, 64d, 66d, 67d, 63d });
    }

    private static CrossSectionLongitudinalGeometry[] BuildGeometries(
        double[] distances,
        double[] bottomLevels)
    {
        var sections = CreateSections(distances, bottomLevels);
        var sequence = new CrossSectionSequence(sections);
        var pairs = new CrossSectionAdjacentPairBuilder().Build(sequence);
        var records = sections.ToDictionary(section => section.Number);
        var builder = new CrossSectionLongitudinalGeometryBuilder();

        return pairs
            .Select(pair => builder.Build(
                pair,
                records[pair.First.Number],
                records[pair.Second.Number]))
            .ToArray();
    }

    private static CrossSectionRecord[] CreateSections(
        double[] distances,
        double[] bottomLevels)
    {
        var profiles = new[]
        {
            TestCrossSectionFactory.Symmetric(),
            TestCrossSectionFactory.Asymmetric(),
            TestCrossSectionFactory.Incomplete(),
            TestCrossSectionFactory.FlatBottom(),
            TestCrossSectionFactory.SlopedBottom()
        };

        return profiles
            .Take(distances.Length)
            .Select((profile, index) =>
            {
                profile.Number = index + 1;
                profile.DistanceFromHydroUnitM =
                    distances[index].ToString(CultureInfo.InvariantCulture);
                profile.BottomLevelZb =
                    bottomLevels[index].ToString(CultureInfo.InvariantCulture);
                return profile;
            })
            .ToArray();
    }

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
