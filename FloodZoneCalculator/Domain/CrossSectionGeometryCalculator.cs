using System;

namespace FloodZoneCalculator.Domain;

public sealed class CrossSectionGeometryCalculator
{
    public CrossSectionGeometryCalculationResult Calculate(
        CrossSectionGeometry geometry,
        double waterLevel)
    {
        if (geometry == null) throw new ArgumentNullException(nameof(geometry));

        double area = 0;
        double perimeter = 0;

        foreach (var segment in geometry.Segments)
        {
            var start = segment.Start;
            var end = segment.End;
            var startBelow = start.ElevationM <= waterLevel;
            var endBelow = end.ElevationM <= waterLevel;

            if (!startBelow && !endBelow)
                continue;

            var wetStart = start;
            var wetEnd = end;

            if (startBelow != endBelow)
            {
                var t = (waterLevel - start.ElevationM) /
                        (end.ElevationM - start.ElevationM);
                var intersection = (
                    DistanceM: start.DistanceM + t * (end.DistanceM - start.DistanceM),
                    ElevationM: waterLevel);

                if (startBelow)
                    wetEnd = new ProfilePoint(intersection.DistanceM, intersection.ElevationM, "", "", 0);
                else
                    wetStart = new ProfilePoint(intersection.DistanceM, intersection.ElevationM, "", "", 0);
            }

            var dx = wetEnd.DistanceM - wetStart.DistanceM;
            var dz = wetEnd.ElevationM - wetStart.ElevationM;
            var depthStart = Math.Max(0, waterLevel - wetStart.ElevationM);
            var depthEnd = Math.Max(0, waterLevel - wetEnd.ElevationM);

            area += (depthStart + depthEnd) / 2 * Math.Abs(dx);
            perimeter += Math.Sqrt(dx * dx + dz * dz);
        }

        return new CrossSectionGeometryCalculationResult(
            waterLevel,
            area,
            perimeter);
    }

}
