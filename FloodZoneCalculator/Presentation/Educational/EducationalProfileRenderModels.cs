#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FloodZoneCalculator.Presentation.Educational;

public sealed class EducationalPlotPoint
{
    public EducationalPlotPoint(double x, double y, long sourceIsodatId)
    {
        X = x;
        Y = y;
        SourceIsodatId = sourceIsodatId;
    }

    public double X { get; }
    public double Y { get; }
    public long SourceIsodatId { get; }
}

public sealed class EducationalPlotSegment
{
    public EducationalPlotSegment(EducationalPlotPoint start, EducationalPlotPoint end)
    {
        Start = start ?? throw new ArgumentNullException(nameof(start));
        End = end ?? throw new ArgumentNullException(nameof(end));
    }

    public EducationalPlotPoint Start { get; }
    public EducationalPlotPoint End { get; }
}

public sealed class EducationalProfileRenderFrame
{
    public EducationalProfileRenderFrame(
        IEnumerable<EducationalPlotPoint> points,
        IEnumerable<EducationalPlotSegment> segments,
        string xAxisTitle,
        string yAxisTitle,
        double? referenceDepth,
        string? referenceLabel)
    {
        Points = Array.AsReadOnly(points.ToArray());
        Segments = Array.AsReadOnly(segments.ToArray());
        XAxisTitle = xAxisTitle;
        YAxisTitle = yAxisTitle;
        ReferenceDepth = referenceDepth;
        ReferenceLabel = referenceLabel;
    }

    public IReadOnlyList<EducationalPlotPoint> Points { get; }
    public IReadOnlyList<EducationalPlotSegment> Segments { get; }
    public string XAxisTitle { get; }
    public string YAxisTitle { get; }
    public double? ReferenceDepth { get; }
    public string? ReferenceLabel { get; }
}

public sealed class EducationalDepthProfileRenderer
{
    public EducationalProfileRenderFrame Prepare(EducationalHydraulicProfileData profile)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        var points = profile.DepthPoints
            .Select(point => new EducationalPlotPoint(
                (double)point.X,
                (double)point.DepthH,
                point.SourceIsodatId))
            .OrderBy(point => point.X)
            .ToArray();
        return new EducationalProfileRenderFrame(
            points,
            CreateSegments(points),
            "X — Ri / DistanceM, учебная координата",
            "Y — Depth, м",
            profile.HydraulicResult.EducationalWaterDepth,
            "Учебный уровень глубины = "
                + profile.HydraulicResult.EducationalWaterDepth.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
                + " м");
    }

    private static IEnumerable<EducationalPlotSegment> CreateSegments(
        IReadOnlyList<EducationalPlotPoint> points)
    {
        for (var index = 1; index < points.Count; index++)
            yield return new EducationalPlotSegment(points[index - 1], points[index]);
    }
}

public sealed class EducationalVelocityProfileRenderer
{
    public EducationalProfileRenderFrame Prepare(EducationalHydraulicProfileData profile)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));

        var points = profile.VelocityPoints
            .Select(point => new EducationalPlotPoint(
                (double)point.X,
                (double)point.ObservedVelocity!.Value,
                point.SourceIsodatId))
            .OrderBy(point => point.X)
            .ToArray();
        var segments = Enumerable.Range(1, Math.Max(0, points.Length - 1))
            .Select(index => new EducationalPlotSegment(points[index - 1], points[index]));
        return new EducationalProfileRenderFrame(
            points,
            segments,
            "X — Ri / DistanceM, учебная координата",
            "Y — ObservedVelocity, м/с",
            null,
            null);
    }
}
