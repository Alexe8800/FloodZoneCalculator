using FloodZoneCalculator.Presentation.Visualization;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class CrossSectionProfileRendererTests
{
    [Fact]
    public void Prepare_Profile01UsesNineExistingPointsAndEightExistingSegments()
    {
        var model = CreateModel(
            new[]
            {
                new VisualizationProfilePoint(0, 110, "Left", "Bank", 1),
                new VisualizationProfilePoint(20, 108, "Left", "Horizontal", 2),
                new VisualizationProfilePoint(40, 105, "Left", "ChannelBank", 3),
                new VisualizationProfilePoint(60, 100, "Left", "Bottom", 4),
                new VisualizationProfilePoint(80, 98, "Center", "Bottom", 5),
                new VisualizationProfilePoint(100, 100, "Right", "Bottom", 6),
                new VisualizationProfilePoint(120, 105, "Right", "ChannelBank", 7),
                new VisualizationProfilePoint(140, 108, "Right", "Horizontal", 8),
                new VisualizationProfilePoint(160, 110, "Right", "Bank", 9)
            });

        var frame = new CrossSectionProfileRenderer().Prepare(model);

        Assert.Equal(8, frame.ProfileSegments.Count);
        Assert.Equal(model.WaterLevel, frame.WaterLevel);
        Assert.Equal(
            model.Segments.Select(segment =>
                (segment.Start.X, segment.Start.Z, segment.End.X, segment.End.Z)),
            frame.ProfileSegments.Select(segment =>
                (segment.Start.X, segment.Start.Z, segment.End.X, segment.End.Z)));
        Assert.Equal(9, model.ProfilePoints.Count);
        Assert.Equal(8, model.Segments.Count);
    }

    [Fact]
    public void Prepare_Profile03IncompleteRemainsFivePoints()
    {
        var model = CreateModel(
            new[]
            {
                new VisualizationProfilePoint(0, 110, "Left", "Bank", 1),
                new VisualizationProfilePoint(30, 106, "Left", "Horizontal", 2),
                new VisualizationProfilePoint(70, 98, "Center", "Bottom", 3),
                new VisualizationProfilePoint(130, 106, "Right", "Horizontal", 4),
                new VisualizationProfilePoint(160, 110, "Right", "Bank", 5)
            });

        var frame = new CrossSectionProfileRenderer().Prepare(model);

        Assert.Equal(5, model.ProfilePoints.Count);
        Assert.Equal(4, model.Segments.Count);
        Assert.Equal(4, frame.ProfileSegments.Count);
        Assert.DoesNotContain(model.ProfilePoints, point => point.PointNumber > 5);
    }

    [Fact]
    public void Prepare_ClippingPointsStayOnlyInRenderFrame()
    {
        var points = new[]
        {
            new VisualizationProfilePoint(0, 110, "Left", "Bank", 1),
            new VisualizationProfilePoint(10, 90, "Center", "Bottom", 2)
        };
        var model = CreateModel(points, 100);

        var frame = new CrossSectionProfileRenderer().Prepare(model);

        Assert.Equal(2, model.ProfilePoints.Count);
        Assert.Contains(frame.WetAreaPolygons, segment =>
            segment.Start.Z == 100 || segment.End.Z == 100);
        Assert.DoesNotContain(model.ProfilePoints, point => point.Z == 100);
        Assert.Equal(5, frame.WetAreaPolygons[0].Start.X);
    }

    [Fact]
    public void Prepare_DoesNotRecalculateAreaOrHydraulicValues()
    {
        var model = CreateModel(
            new[]
            {
                new VisualizationProfilePoint(0, 110, "Left", "Bank", 1),
                new VisualizationProfilePoint(10, 90, "Center", "Bottom", 2)
            },
            100);

        var values = new[]
        {
            model.Area,
            model.GeometricPerimeterBelowWaterLevel,
            model.Omega,
            model.Chi,
            model.HydraulicRadius,
            model.HydraulicSlope,
            model.ShearVelocity,
            model.ChezyCoefficient,
            model.Velocity,
            model.Discharge
        };

        _ = new CrossSectionProfileRenderer().Prepare(model);

        Assert.Equal(values[0], model.Area);
        Assert.Equal(values[1], model.GeometricPerimeterBelowWaterLevel);
        Assert.Equal(values[2], model.Omega);
        Assert.Equal(values[3], model.Chi);
        Assert.Equal(values[4], model.HydraulicRadius);
        Assert.Equal(values[5], model.HydraulicSlope);
        Assert.Equal(values[6], model.ShearVelocity);
        Assert.Equal(values[7], model.ChezyCoefficient);
        Assert.Equal(values[8], model.Velocity);
        Assert.Equal(values[9], model.Discharge);
    }

    [Fact]
    public void Prepare_IsRepeatableAndDoesNotChangeCoordinates()
    {
        var model = CreateModel(
            new[]
            {
                new VisualizationProfilePoint(0, 110, "Left", "Bank", 1),
                new VisualizationProfilePoint(10, 90, "Center", "Bottom", 2)
            },
            100);
        var original = model.ProfilePoints
            .Select(point => (point.X, point.Z))
            .ToArray();

        var first = new CrossSectionProfileRenderer().Prepare(model);
        var second = new CrossSectionProfileRenderer().Prepare(model);

        Assert.Equal(original, model.ProfilePoints.Select(point => (point.X, point.Z)));
        Assert.Equal(
            first.ProfileSegments.Select(segment => (segment.Start.X, segment.End.X)),
            second.ProfileSegments.Select(segment => (segment.Start.X, segment.End.X)));
        Assert.Equal(
            first.WetAreaPolygons.Select(segment => (segment.Start.X, segment.End.X)),
            second.WetAreaPolygons.Select(segment => (segment.Start.X, segment.End.X)));
    }

    private static CrossSectionVisualizationModel CreateModel(
        IReadOnlyList<VisualizationProfilePoint> points,
        double waterLevel = 105)
    {
        var segments = points
            .Zip(points.Skip(1), (start, end) =>
                new VisualizationProfileSegment(start, end))
            .ToArray();

        return new CrossSectionVisualizationModel(
            1,
            0,
            waterLevel,
            points,
            segments,
            12,
            34,
            12,
            34,
            0.35,
            0.001,
            0.2,
            20,
            0.7,
            8.4);
    }
}
