using System;
using System.Collections.Generic;
using System.Linq;

namespace FloodZoneCalculator.Presentation.Visualization;

public sealed class VisualizationRenderPoint
{
    public VisualizationRenderPoint(double x, double z)
    {
        X = x;
        Z = z;
    }

    public double X { get; }
    public double Z { get; }
}

public sealed class VisualizationRenderSegment
{
    public VisualizationRenderSegment(
        VisualizationRenderPoint start,
        VisualizationRenderPoint end)
    {
        Start = start ?? throw new ArgumentNullException(nameof(start));
        End = end ?? throw new ArgumentNullException(nameof(end));
    }

    public VisualizationRenderPoint Start { get; }
    public VisualizationRenderPoint End { get; }
}

public sealed class CrossSectionProfileRenderFrame
{
    public CrossSectionProfileRenderFrame(
        IEnumerable<VisualizationRenderSegment> profileSegments,
        IEnumerable<VisualizationRenderSegment> wetAreaPolygons,
        double waterLevel)
    {
        ProfileSegments = ReadOnly(profileSegments, nameof(profileSegments));
        WetAreaPolygons = ReadOnly(wetAreaPolygons, nameof(wetAreaPolygons));
        WaterLevel = waterLevel;
    }

    public IReadOnlyList<VisualizationRenderSegment> ProfileSegments { get; }
    public IReadOnlyList<VisualizationRenderSegment> WetAreaPolygons { get; }
    public double WaterLevel { get; }

    private static IReadOnlyList<T> ReadOnly<T>(
        IEnumerable<T> values,
        string parameterName)
    {
        if (values == null)
            throw new ArgumentNullException(parameterName);

        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed class CrossSectionProfileRenderer
{
    public CrossSectionProfileRenderFrame Prepare(
        CrossSectionVisualizationModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        var profileSegments = model.Segments
            .Select(segment => new VisualizationRenderSegment(
                new VisualizationRenderPoint(segment.Start.X, segment.Start.Z),
                new VisualizationRenderPoint(segment.End.X, segment.End.Z)))
            .ToArray();

        var wetAreaPolygons = model.Segments
            .SelectMany(segment => ClipBelowWaterLevel(segment, model.WaterLevel))
            .ToArray();

        return new CrossSectionProfileRenderFrame(
            profileSegments,
            wetAreaPolygons,
            model.WaterLevel);
    }

    private static IEnumerable<VisualizationRenderSegment> ClipBelowWaterLevel(
        VisualizationProfileSegment segment,
        double waterLevel)
    {
        var start = new VisualizationRenderPoint(segment.Start.X, segment.Start.Z);
        var end = new VisualizationRenderPoint(segment.End.X, segment.End.Z);
        var startBelow = start.Z <= waterLevel;
        var endBelow = end.Z <= waterLevel;

        if (!startBelow && !endBelow)
            yield break;

        if (startBelow && endBelow)
        {
            yield return new VisualizationRenderSegment(start, end);
            yield break;
        }

        var intersection = IntersectAtWaterLevel(start, end, waterLevel);
        if (startBelow)
            yield return new VisualizationRenderSegment(start, intersection);
        else
            yield return new VisualizationRenderSegment(intersection, end);
    }

    private static VisualizationRenderPoint IntersectAtWaterLevel(
        VisualizationRenderPoint start,
        VisualizationRenderPoint end,
        double waterLevel)
    {
        var dz = end.Z - start.Z;
        if (dz == 0)
            return new VisualizationRenderPoint(start.X, waterLevel);

        var fraction = (waterLevel - start.Z) / dz;
        return new VisualizationRenderPoint(
            start.X + fraction * (end.X - start.X),
            waterLevel);
    }
}
