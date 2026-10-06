using FloodZoneCalculator.Domain;
using FloodZoneCalculator.Presentation.Visualization;
using FloodZoneDb.Client;
using System.Globalization;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class LongitudinalProfileRendererTests
{
    [Fact]
    public void Prepare_ProjectsAllFiveProfilesInSpatialOrderUsingExistingSegments()
    {
        var model = CreateModel();

        var frame = new LongitudinalProfileRenderer().Prepare(model);

        Assert.Equal(5, frame.Points.Count);
        Assert.Equal(4, frame.Segments.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 },
            frame.Points.Select(point => point.Number));
        Assert.Equal(new[] { 0d, 1000d, 2500d, 4000d, 7000d },
            frame.Points.Select(point => point.X));
        Assert.Equal(new[] { 65d, 64d, 64d, 65d, 63d },
            frame.Points.Select(point => point.Y));

        Assert.Equal(new[] { 1, 2, 3, 4 },
            frame.Segments.Select(segment => segment.Start.Number));
        Assert.Equal(new[] { 2, 3, 4, 5 },
            frame.Segments.Select(segment => segment.End.Number));
        Assert.Equal(new[] { 1000d, 1500d, 1500d, 3000d },
            frame.Segments.Select(segment => segment.DeltaDistanceM));
        Assert.Equal(new[] { -1d, 0d, 1d, -2d },
            frame.Segments.Select(segment => segment.BottomLevelDifferenceM));
        Assert.Equal(new[] { -0.001d, 0d, 1d / 1500d, -2d / 3000d },
            frame.Segments.Select(segment => segment.NeutralGeometryRatio));
    }

    [Fact]
    public void Prepare_SortsArbitraryInputOrderByDistanceAndKeepsNumberAsIdentity()
    {
        var model = CreateModel(inputOrder: new[] { 4, 1, 5, 3, 2 });

        var frame = new LongitudinalProfileRenderer().Prepare(model);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 },
            frame.Points.Select(point => point.Number));
        Assert.Equal(new[] { 1, 2, 3, 4 },
            frame.Segments.Select(segment => segment.Start.Number));
    }

    [Fact]
    public void Prepare_DoesNotMutateSourceAndIsRepeatable()
    {
        var model = CreateModel(inputOrder: new[] { 4, 1, 5, 3, 2 });
        var sectionOrderBefore = model.Longitudinal.Sections
            .Select(section => section.Number).ToArray();
        var sectionValuesBefore = model.Longitudinal.Sections
            .Select(section => (section.Number, section.DistanceFromHydroUnitM,
                section.BottomLevelZb)).ToArray();
        var segmentValuesBefore = model.Longitudinal.Segments
            .Select(segment => (segment.FirstNumber, segment.SecondNumber,
                segment.DeltaDistanceM, segment.BottomLevelDifferenceM,
                segment.NeutralGeometryRatio)).ToArray();

        var renderer = new LongitudinalProfileRenderer();
        var first = renderer.Prepare(model);
        var second = renderer.Prepare(model);

        Assert.Equal(sectionOrderBefore,
            model.Longitudinal.Sections.Select(section => section.Number));
        Assert.Equal(sectionValuesBefore,
            model.Longitudinal.Sections.Select(section => (
                section.Number,
                section.DistanceFromHydroUnitM,
                section.BottomLevelZb)));
        Assert.Equal(segmentValuesBefore,
            model.Longitudinal.Segments.Select(segment => (
                segment.FirstNumber,
                segment.SecondNumber,
                segment.DeltaDistanceM,
                segment.BottomLevelDifferenceM,
                segment.NeutralGeometryRatio)));
        Assert.Equal(
            first.Points.Select(point => (point.Number, point.X, point.Y)),
            second.Points.Select(point => (point.Number, point.X, point.Y)));
        Assert.Equal(
            first.Segments.Select(segment => (
                segment.Start.Number,
                segment.End.Number,
                segment.DeltaDistanceM,
                segment.BottomLevelDifferenceM,
                segment.NeutralGeometryRatio)),
            second.Segments.Select(segment => (
                segment.Start.Number,
                segment.End.Number,
                segment.DeltaDistanceM,
                segment.BottomLevelDifferenceM,
                segment.NeutralGeometryRatio)));

        Assert.Equal(5, model.CrossSections.Single(
            section => section.CrossSectionNumber == 3).ProfilePoints.Count);
        Assert.Equal(new[] { 9, 9, 5, 9, 9 },
            model.CrossSections
                .OrderBy(section => section.CrossSectionNumber)
                .Select(section => section.ProfilePoints.Count));
    }

    [Fact]
    public void Prepare_WhenProfile02DistanceChanges_ReordersOnlyItsPositionAndCopiesExistingRatio()
    {
        var model = CreateModel(profile2Distance: 6000);

        var frame = new LongitudinalProfileRenderer().Prepare(model);

        Assert.Equal(new[] { 1, 3, 4, 2, 5 },
            frame.Points.Select(point => point.Number));
        Assert.Equal(64, frame.Points.Single(point => point.Number == 2).Y);
        Assert.Equal(-0.0005,
            frame.Segments.Single(segment =>
                segment.Start.Number == 4 && segment.End.Number == 2)
                .NeutralGeometryRatio);
        Assert.Equal(-0.001,
            frame.Segments.Single(segment =>
                segment.Start.Number == 2 && segment.End.Number == 5)
                .NeutralGeometryRatio);

        var originalProfile2 = model.CrossSections.Single(
            section => section.CrossSectionNumber == 2);
        Assert.Equal(10, originalProfile2.Area);
        Assert.Equal(20, originalProfile2.Chi);
        Assert.Equal(0.5, originalProfile2.HydraulicRadius);
        Assert.Equal(0.7, originalProfile2.Velocity);
        Assert.Equal(8.8, originalProfile2.Discharge);
    }

    [Fact]
    public void Prepare_DoesNotInventIntermediateSectionsOrCalculateRatios()
    {
        var model = CreateModel();
        var sourceRatios = model.Longitudinal.Segments
            .Select(segment => segment.NeutralGeometryRatio).ToArray();

        var frame = new LongitudinalProfileRenderer().Prepare(model);

        Assert.Equal(5, frame.Points.Count);
        Assert.Equal(sourceRatios, frame.Segments
            .Select(segment => segment.NeutralGeometryRatio));
    }

    private static HydraulicVisualizationModel CreateModel(
        int[]? inputOrder = null,
        double profile2Distance = 1000)
    {
        var records = new[]
        {
            TestCrossSectionFactory.Symmetric(),
            TestCrossSectionFactory.Asymmetric(),
            TestCrossSectionFactory.Incomplete(),
            TestCrossSectionFactory.FlatBottom(),
            TestCrossSectionFactory.SlopedBottom()
        };
        var distances = new[] { 0d, profile2Distance, 2500d, 4000d, 7000d };
        var bottoms = new[] { 65d, 64d, 64d, 65d, 63d };
        var models = new CrossSectionVisualizationModel[records.Length];

        for (var index = 0; index < records.Length; index++)
        {
            records[index].Number = index + 1;
            records[index].DistanceFromHydroUnitM =
                distances[index].ToString(CultureInfo.InvariantCulture);
            records[index].BottomLevelZb =
                bottoms[index].ToString(CultureInfo.InvariantCulture);
            var geometry = new FloodZoneCalculator.Domain.CrossSectionGeometryBuilder()
                .Build(records[index]);
            var profilePoints = geometry.Points.Select(point =>
                new VisualizationProfilePoint(
                    point.DistanceM,
                    point.ElevationM,
                    point.Side,
                    point.PointType,
                    point.PointNumber)).ToArray();
            var profileSegments = geometry.Segments.Select(segment =>
                new VisualizationProfileSegment(
                    profilePoints.Single(point =>
                        point.PointNumber == segment.Start.PointNumber &&
                        point.Side == segment.Start.Side &&
                        point.X == segment.Start.DistanceM &&
                        point.Z == segment.Start.ElevationM),
                    profilePoints.Single(point =>
                        point.PointNumber == segment.End.PointNumber &&
                        point.Side == segment.End.Side &&
                        point.X == segment.End.DistanceM &&
                        point.Z == segment.End.ElevationM))).ToArray();

            models[index] = new CrossSectionVisualizationModel(
                index + 1,
                distances[index],
                106,
                profilePoints,
                profileSegments,
                10,
                20,
                10,
                20,
                0.5,
                0.001,
                0.1,
                30,
                0.7,
                8.8);
        }

        var spatialOrder = Enumerable.Range(1, records.Length)
            .OrderBy(number => distances[number - 1])
            .ToArray();
        var longitudinalSections = spatialOrder
            .Select(number => new LongitudinalVisualizationSection(
                number,
                distances[number - 1],
                bottoms[number - 1]))
            .ToArray();
        var sequence = new CrossSectionSequence(records);
        var pairBuilder = new CrossSectionAdjacentPairBuilder();
        var longitudinalGeometryBuilder = new CrossSectionLongitudinalGeometryBuilder();
        var ratioCalculator = new LongitudinalGeometryRatioCalculator();
        var recordsByNumber = records.ToDictionary(record => record.Number);
        var longitudinalSegments = pairBuilder.Build(sequence)
            .Select(pair =>
            {
                var geometry = longitudinalGeometryBuilder.Build(
                    pair,
                    recordsByNumber[pair.First.Number],
                    recordsByNumber[pair.Second.Number]);
                return new LongitudinalVisualizationSegment(
                    pair.First.Number,
                    pair.Second.Number,
                    geometry.DeltaDistanceM,
                    geometry.BottomLevelDifferenceM,
                    ratioCalculator.Calculate(geometry));
            })
            .ToArray();

        if (inputOrder != null)
            longitudinalSections = inputOrder
                .Select(number => longitudinalSections.Single(
                    section => section.Number == number))
                .ToArray();

        return new HydraulicVisualizationModel(
            models,
            new LongitudinalVisualizationModel(
                longitudinalSections,
                longitudinalSegments));
    }
}
