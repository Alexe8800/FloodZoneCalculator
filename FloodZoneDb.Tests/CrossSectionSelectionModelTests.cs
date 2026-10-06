using FloodZoneCalculator.Domain;
using FloodZoneCalculator.Presentation.Visualization;
using System.Globalization;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class CrossSectionSelectionModelTests
{
    [Fact]
    public void Constructor_SortsAvailableSectionsByDistanceNotNumber()
    {
        var model = CreateHydraulicModel(
            CreateSection(4, 4000),
            CreateSection(1, 0),
            CreateSection(5, 7000),
            CreateSection(3, 2500),
            CreateSection(2, 1000));

        var selection = new CrossSectionSelectionModel(model);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 },
            selection.Sections.Select(item => item.Number));
        Assert.Equal(new[] { 0d, 1000d, 2500d, 4000d, 7000d },
            selection.Sections.Select(item => item.DistanceFromHydroUnitM));
    }

    [Fact]
    public void Constructor_DoesNotUseNumberAsSpatialOrder()
    {
        var selection = new CrossSectionSelectionModel(CreateHydraulicModel(
            CreateSection(10, 2500),
            CreateSection(30, 0),
            CreateSection(20, 1000)));

        Assert.Equal(new[] { 30, 20, 10 },
            selection.Sections.Select(item => item.Number));
    }

    [Theory]
    [InlineData(1, 9)]
    [InlineData(2, 9)]
    [InlineData(3, 5)]
    [InlineData(4, 9)]
    [InlineData(5, 9)]
    public void GetSection_ReturnsOriginalSectionWithExpectedProfile(
        int number,
        int expectedPointCount)
    {
        var source = Enumerable.Range(1, 5)
            .Select(sectionNumber => CreateSection(
                sectionNumber,
                (sectionNumber - 1) * 1000,
                sectionNumber == 3 ? 5 : 9))
            .ToArray();
        var model = CreateHydraulicModel(source);
        var selection = new CrossSectionSelectionModel(model);

        var selected = selection.GetSection(number);

        Assert.Same(source[number - 1], selected);
        Assert.Equal(number, selected.CrossSectionNumber);
        Assert.Equal(expectedPointCount, selected.ProfilePoints.Count);
    }

    [Fact]
    public void Constructor_ExposesAllFiveSyntheticProfilesWithoutChangingTheirPoints()
    {
        var source = CreateVisualizationProfiles();
        var originalCoordinates = source.ToDictionary(
            section => section.CrossSectionNumber,
            section => section.ProfilePoints.Select(point => (point.X, point.Z)).ToArray());

        var selection = new CrossSectionSelectionModel(CreateHydraulicModel(source));

        Assert.Equal(5, selection.Sections.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 },
            selection.Sections.Select(item => item.Number));
        foreach (var item in selection.Sections)
        {
            var selected = selection.GetSection(item.Number);
            Assert.Same(source.Single(section =>
                section.CrossSectionNumber == item.Number), selected);
            Assert.Equal(originalCoordinates[item.Number],
                selected.ProfilePoints.Select(point => (point.X, point.Z)));
        }

        Assert.Equal(5, selection.GetSection(3).ProfilePoints.Count);
    }

    [Fact]
    public void Constructor_DoesNotMutateSourceCollectionOrVisualizationModels()
    {
        var first = CreateSection(4, 4000);
        var second = CreateSection(1, 0, 5);
        var third = CreateSection(2, 1000);
        var sourceSections = new[] { first, second, third };
        var sourcePoints = sourceSections.ToDictionary(
            section => section.CrossSectionNumber,
            section => section.ProfilePoints.Select(point => (point.X, point.Z)).ToArray());
        var model = CreateHydraulicModel(sourceSections);

        var selection = new CrossSectionSelectionModel(model);

        Assert.Equal(new[] { 4, 1, 2 },
            sourceSections.Select(section => section.CrossSectionNumber));
        foreach (var section in sourceSections)
        {
            Assert.Equal(
                sourcePoints[section.CrossSectionNumber],
                section.ProfilePoints.Select(point => (point.X, point.Z)));
        }
        Assert.Equal(new[] { 1, 2, 4 },
            selection.Sections.Select(item => item.Number));
    }

    [Fact]
    public void ChangedDistance_ChangesSpatialPositionWithoutChangingHydraulicValues()
    {
        var section1 = CreateSection(1, 0);
        var profile2At1000 = CreateSection(2, 1000);
        var section3 = CreateSection(3, 2500);
        var section4 = CreateSection(4, 4000);
        var section5 = CreateSection(5, 7000);

        var initialSelection = new CrossSectionSelectionModel(
            CreateHydraulicModel(section1, profile2At1000, section3, section4, section5));

        var profile2At6000 = CreateSection(
            2,
            6000,
            profile2At1000.ProfilePoints.Count,
            profile2At1000);
        var updatedSelection = new CrossSectionSelectionModel(
            CreateHydraulicModel(section1, profile2At6000, section3, section4, section5));

        Assert.Equal(new[] { 1, 2, 3, 4, 5 },
            initialSelection.Sections.Select(item => item.Number));
        Assert.Equal(new[] { 1, 3, 4, 2, 5 },
            updatedSelection.Sections.Select(item => item.Number));

        var before = initialSelection.GetSection(2);
        var after = updatedSelection.GetSection(2);
        Assert.Equal(1000, before.DistanceFromHydroUnitM);
        Assert.Equal(6000, after.DistanceFromHydroUnitM);
        Assert.Equal(before.WaterLevel, after.WaterLevel);
        Assert.Equal(before.Area, after.Area);
        Assert.Equal(before.Omega, after.Omega);
        Assert.Equal(before.Chi, after.Chi);
        Assert.Equal(before.HydraulicRadius, after.HydraulicRadius);
        Assert.Equal(before.HydraulicSlope, after.HydraulicSlope);
        Assert.Equal(before.ShearVelocity, after.ShearVelocity);
        Assert.Equal(before.ChezyCoefficient, after.ChezyCoefficient);
        Assert.Equal(before.Velocity, after.Velocity);
        Assert.Equal(before.Discharge, after.Discharge);
    }

    [Fact]
    public void GetSection_UsesNumberAsIdentityAndReturnsTheSameVisualizationModel()
    {
        var section = CreateSection(30, 1000);
        var selection = new CrossSectionSelectionModel(CreateHydraulicModel(section));

        Assert.Same(section, selection.GetSection(30));
        Assert.Throws<ArgumentOutOfRangeException>(() => selection.GetSection(10));
    }

    private static HydraulicVisualizationModel CreateHydraulicModel(
        params CrossSectionVisualizationModel[] sections) =>
        new(
            sections,
            new LongitudinalVisualizationModel(
                Array.Empty<LongitudinalVisualizationSection>(),
                Array.Empty<LongitudinalVisualizationSegment>()));

    private static CrossSectionVisualizationModel CreateSection(
        int number,
        double distance,
        int pointCount = 9,
        CrossSectionVisualizationModel? source = null)
    {
        var points = source?.ProfilePoints.ToArray() ??
            Enumerable.Range(1, pointCount)
                .Select(index => new VisualizationProfilePoint(
                    index * 10,
                    110 - index,
                    index <= pointCount / 2 ? "Left" : "Right",
                    "Bank",
                    index))
                .ToArray();
        var segments = points.Zip(points.Skip(1), (start, end) =>
            new VisualizationProfileSegment(start, end)).ToArray();

        return new CrossSectionVisualizationModel(
            number,
            distance,
            106,
            points,
            segments,
            11,
            22,
            11,
            22,
            0.5,
            0.001,
            0.1,
            30,
            0.8,
            8.8);
    }

    private static CrossSectionVisualizationModel[] CreateVisualizationProfiles()
    {
        var records = new[]
        {
            TestCrossSectionFactory.Symmetric(),
            TestCrossSectionFactory.Asymmetric(),
            TestCrossSectionFactory.Incomplete(),
            TestCrossSectionFactory.FlatBottom(),
            TestCrossSectionFactory.SlopedBottom()
        };
        var distances = new[] { 0, 1000, 2500, 4000, 7000 };
        var geometryBuilder = new CrossSectionGeometryBuilder();

        return records.Select((record, index) =>
        {
            record.Number = index + 1;
            record.DistanceFromHydroUnitM =
                distances[index].ToString(CultureInfo.InvariantCulture);
            var geometry = geometryBuilder.Build(record);
            var points = geometry.Points.Select(point =>
                new VisualizationProfilePoint(
                    point.DistanceM,
                    point.ElevationM,
                    point.Side,
                    point.PointType,
                    point.PointNumber)).ToArray();
            var segments = geometry.Segments.Select(segment =>
                new VisualizationProfileSegment(
                    points.Single(point =>
                        point.PointNumber == segment.Start.PointNumber &&
                        point.Side == segment.Start.Side &&
                        point.X == segment.Start.DistanceM &&
                        point.Z == segment.Start.ElevationM),
                    points.Single(point =>
                        point.PointNumber == segment.End.PointNumber &&
                        point.Side == segment.End.Side &&
                        point.X == segment.End.DistanceM &&
                        point.Z == segment.End.ElevationM))).ToArray();

            return new CrossSectionVisualizationModel(
                geometry.Number,
                geometry.DistanceFromHydroUnitM,
                106,
                points,
                segments,
                12,
                24,
                12,
                24,
                0.5,
                0.001,
                0.2,
                20,
                0.7,
                8.4);
        }).ToArray();
    }
}
