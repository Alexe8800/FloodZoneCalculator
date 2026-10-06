using FloodZoneCalculator.Presentation.Visualization;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class HydraulicVisualizationStateTests
{
    [Fact]
    public void State_KeepsSourceModelAndSelectionCount()
    {
        var source = CreateModel();

        var state = new HydraulicVisualizationState(source);

        Assert.Same(source, state.Model);
        Assert.Equal(5, state.Selection.Sections.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 },
            state.Selection.Sections.Select(section => section.Number));
    }

    [Fact]
    public void State_SelectsSection01And03WithoutChangingData()
    {
        var source = CreateModel();
        var state = new HydraulicVisualizationState(source);

        var section01State = state.SelectSection(1);
        var section03State = state.SelectSection(3);

        Assert.Same(source.CrossSections[0], section01State.SelectedSection);
        Assert.Equal(1, section01State.SelectedSection!.CrossSectionNumber);
        Assert.Equal(0, section01State.SelectedSection.DistanceFromHydroUnitM);
        Assert.Same(source.CrossSections[2], section03State.SelectedSection);
        Assert.Equal(3, section03State.SelectedSection!.CrossSectionNumber);
        Assert.Equal(5, section03State.SelectedSection.ProfilePoints.Count);
        Assert.Equal(new[] { 9, 9, 5, 9, 9 },
            source.CrossSections.Select(section => section.ProfilePoints.Count));
    }

    [Fact]
    public void State_PreservesLongitudinalAndHydraulicValuesAndIsRepeatable()
    {
        var source = CreateModel();
        var originalLongitudinal = source.Longitudinal.Segments
            .Select(segment => (segment.FirstNumber, segment.SecondNumber,
                segment.DeltaDistanceM, segment.BottomLevelDifferenceM,
                segment.NeutralGeometryRatio)).ToArray();
        var originalHydraulics = source.CrossSections
            .Select(section => (section.CrossSectionNumber, section.Area,
                section.GeometricPerimeterBelowWaterLevel, section.Omega,
                section.Chi, section.HydraulicRadius, section.HydraulicSlope,
                section.ShearVelocity, section.ChezyCoefficient,
                section.Velocity, section.Discharge)).ToArray();

        var firstOpenState = new HydraulicVisualizationState(source).SelectSection(3);
        var reopenedState = new HydraulicVisualizationState(source).SelectSection(3);
        var switchedState = firstOpenState.SelectSection(1);
        var switchedBackState = switchedState.SelectSection(3);

        Assert.Same(firstOpenState.SelectedSection, reopenedState.SelectedSection);
        Assert.Same(firstOpenState.SelectedSection, switchedBackState.SelectedSection);
        Assert.Equal(originalLongitudinal,
            source.Longitudinal.Segments.Select(segment => (
                segment.FirstNumber,
                segment.SecondNumber,
                segment.DeltaDistanceM,
                segment.BottomLevelDifferenceM,
                segment.NeutralGeometryRatio)));
        Assert.Equal(originalHydraulics,
            source.CrossSections.Select(section => (
                section.CrossSectionNumber,
                section.Area,
                section.GeometricPerimeterBelowWaterLevel,
                section.Omega,
                section.Chi,
                section.HydraulicRadius,
                section.HydraulicSlope,
                section.ShearVelocity,
                section.ChezyCoefficient,
                section.Velocity,
                section.Discharge)));
    }

    [Fact]
    public void State_UsesSpatialOrderAndPreservesLongitudinalValues()
    {
        var source = CreateModel(inputOrder: new[] { 4, 1, 5, 3, 2 });
        var state = new HydraulicVisualizationState(source);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 },
            state.Selection.Sections.Select(section => section.Number));
        Assert.Equal(new[] { 1000d, 1500d, 1500d, 3000d },
            source.Longitudinal.Segments.Select(segment => segment.DeltaDistanceM));
        Assert.Equal(new[] { -0.001d, 0d, 1d / 1500d, -2d / 3000d },
            source.Longitudinal.Segments
                .Select(segment => segment.NeutralGeometryRatio));
    }

    [Fact]
    public void State_HandlesEmptySectionsAndMissingSelectionExplicitly()
    {
        var emptyModel = new HydraulicVisualizationModel(
            Array.Empty<CrossSectionVisualizationModel>(),
            new LongitudinalVisualizationModel(
                Array.Empty<LongitudinalVisualizationSection>(),
                Array.Empty<LongitudinalVisualizationSegment>()));
        var emptyState = new HydraulicVisualizationState(emptyModel);

        Assert.Empty(emptyState.Selection.Sections);
        Assert.Null(emptyState.SelectedSection);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            emptyState.SelectSection(1));
        Assert.Throws<ArgumentNullException>(() =>
            new HydraulicVisualizationState(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HydraulicVisualizationState(CreateModel(), 999));
    }

    private static HydraulicVisualizationModel CreateModel(
        int[]? inputOrder = null)
    {
        var distances = new[] { 0d, 1000d, 2500d, 4000d, 7000d };
        var bottoms = new[] { 65d, 64d, 64d, 65d, 63d };
        var order = inputOrder ?? new[] { 1, 2, 3, 4, 5 };
        var crossSections = order
            .Select(number => CreateCrossSection(number, distances[number - 1]))
            .ToArray();
        var longitudinalSections = order.Select(number =>
            new LongitudinalVisualizationSection(
                number,
                distances[number - 1],
                bottoms[number - 1])).ToArray();
        var spatialOrder = longitudinalSections
            .OrderBy(section => section.DistanceFromHydroUnitM)
            .ToArray();
        var longitudinalSegments = spatialOrder.Zip(
            spatialOrder.Skip(1),
            (first, second) =>
            {
                var delta = second.DistanceFromHydroUnitM -
                    first.DistanceFromHydroUnitM;
                var difference = second.BottomLevelZb - first.BottomLevelZb;
                return new LongitudinalVisualizationSegment(
                    first.Number,
                    second.Number,
                    delta,
                    difference,
                    difference / delta);
            }).ToArray();

        return new HydraulicVisualizationModel(
            crossSections,
            new LongitudinalVisualizationModel(
                longitudinalSections,
                longitudinalSegments));
    }

    private static CrossSectionVisualizationModel CreateCrossSection(
        int number,
        double distance)
    {
        var count = number == 3 ? 5 : 9;
        var points = Enumerable.Range(1, count)
            .Select(index => new VisualizationProfilePoint(
                index * 10,
                110 - index,
                index <= count / 2 ? "Left" : "Right",
                index == 1 || index == count ? "Bank" : "Bottom",
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
            10 + number,
            20 + number,
            10 + number,
            20 + number,
            0.5 + number,
            0.001,
            0.1 + number,
            30 + number,
            0.7 + number,
            8.8 + number);
    }
}
