using System;
using System.Collections.Generic;
using System.Linq;

namespace FloodZoneCalculator.Presentation.Visualization;

public sealed class LongitudinalRenderPoint
{
    public LongitudinalRenderPoint(int number, double x, double y)
    {
        Number = number;
        X = x;
        Y = y;
    }

    public int Number { get; }
    public double X { get; }
    public double Y { get; }
}

public sealed class LongitudinalRenderSegment
{
    public LongitudinalRenderSegment(
        LongitudinalRenderPoint start,
        LongitudinalRenderPoint end,
        double deltaDistanceM,
        double bottomLevelDifferenceM,
        double neutralGeometryRatio)
    {
        Start = start ?? throw new ArgumentNullException(nameof(start));
        End = end ?? throw new ArgumentNullException(nameof(end));
        DeltaDistanceM = deltaDistanceM;
        BottomLevelDifferenceM = bottomLevelDifferenceM;
        NeutralGeometryRatio = neutralGeometryRatio;
    }

    public LongitudinalRenderPoint Start { get; }
    public LongitudinalRenderPoint End { get; }
    public double DeltaDistanceM { get; }
    public double BottomLevelDifferenceM { get; }
    public double NeutralGeometryRatio { get; }
}

public sealed class LongitudinalProfileRenderFrame
{
    public LongitudinalProfileRenderFrame(
        IEnumerable<LongitudinalRenderPoint> points,
        IEnumerable<LongitudinalRenderSegment> segments)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        if (segments == null)
            throw new ArgumentNullException(nameof(segments));

        Points = Array.AsReadOnly(points.ToArray());
        Segments = Array.AsReadOnly(segments.ToArray());
    }

    public IReadOnlyList<LongitudinalRenderPoint> Points { get; }
    public IReadOnlyList<LongitudinalRenderSegment> Segments { get; }
}

public sealed class LongitudinalProfileRenderer
{
    public LongitudinalProfileRenderFrame Prepare(
        HydraulicVisualizationModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        var longitudinal = model.Longitudinal;
        var sortedSections = longitudinal.Sections
            .OrderBy(section => section.DistanceFromHydroUnitM)
            .ToArray();
        var points = sortedSections
            .Select(section => new LongitudinalRenderPoint(
                section.Number,
                section.DistanceFromHydroUnitM,
                section.BottomLevelZb))
            .ToArray();

        var segments = new List<LongitudinalRenderSegment>();
        for (var index = 0; index < sortedSections.Length - 1; index++)
        {
            var first = sortedSections[index];
            var second = sortedSections[index + 1];
            var sourceSegment = longitudinal.Segments.FirstOrDefault(segment =>
                segment.FirstNumber == first.Number &&
                segment.SecondNumber == second.Number);

            if (sourceSegment == null)
            {
                throw new ArgumentException(
                    "Для соседних створов отсутствует готовый продольный сегмент.",
                    nameof(model));
            }

            segments.Add(new LongitudinalRenderSegment(
                points[index],
                points[index + 1],
                sourceSegment.DeltaDistanceM,
                sourceSegment.BottomLevelDifferenceM,
                sourceSegment.NeutralGeometryRatio));
        }

        if (longitudinal.Segments.Count != segments.Count)
        {
            throw new ArgumentException(
                "Набор готовых продольных сегментов не соответствует последовательности створов.",
                nameof(model));
        }

        return new LongitudinalProfileRenderFrame(points, segments);
    }
}
