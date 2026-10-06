using System;
using FloodZoneDb.Client;

namespace FloodZoneCalculator.Domain;

public sealed class EducationalDepthHydraulicCalculator
{
    private readonly HydraulicRadiusCalculator _radiusCalculator;

    public EducationalDepthHydraulicCalculator()
        : this(new HydraulicRadiusCalculator())
    {
    }

    public EducationalDepthHydraulicCalculator(HydraulicRadiusCalculator radiusCalculator)
    {
        _radiusCalculator = radiusCalculator
            ?? throw new ArgumentNullException(nameof(radiusCalculator));
    }

    public EducationalDepthHydraulicResult Calculate(
        EducationalDepthProfile profile,
        double educationalWaterDepth)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));
        if (double.IsNaN(educationalWaterDepth)
            || double.IsInfinity(educationalWaterDepth)
            || educationalWaterDepth < 0)
            throw new ArgumentOutOfRangeException(
                nameof(educationalWaterDepth),
                educationalWaterDepth,
                "Учебный уровень глубины должен быть конечным и неотрицательным.");

        var points = profile.PointsSortedByX;
        var omega = 0.0;
        var chi = 0.0;

        for (var index = 1; index < points.Count; index++)
        {
            var first = points[index - 1];
            var second = points[index];
            var x1 = (double)first.X;
            var x2 = (double)second.X;
            var h1 = (double)first.DepthH;
            var h2 = (double)second.DepthH;
            var firstWithinLevel = h1 <= educationalWaterDepth;
            var secondWithinLevel = h2 <= educationalWaterDepth;

            if (!firstWithinLevel && !secondWithinLevel)
                continue;

            if (firstWithinLevel && secondWithinLevel)
            {
                var dx = Math.Abs(x2 - x1);
                omega += (h1 + h2) / 2.0 * dx;
                chi += SegmentLength(dx, Math.Abs(h2 - h1));
                continue;
            }

            var wetX = firstWithinLevel ? x1 : x2;
            var wetDepth = firstWithinLevel ? h1 : h2;
            var dryX = firstWithinLevel ? x2 : x1;
            var dryDepth = firstWithinLevel ? h2 : h1;
            var crossingFraction = (educationalWaterDepth - wetDepth) / (dryDepth - wetDepth);
            var clippedDx = Math.Abs(dryX - wetX) * crossingFraction;
            var clippedDh = educationalWaterDepth - wetDepth;

            omega += (wetDepth + educationalWaterDepth) / 2.0 * clippedDx;
            chi += SegmentLength(clippedDx, clippedDh);
        }

        if (double.IsNaN(omega) || double.IsInfinity(omega)
            || double.IsNaN(chi) || double.IsInfinity(chi))
            throw new ArithmeticException("Результат учебной геометрии вышел за допустимый числовой диапазон.");

        var radius = _radiusCalculator.Calculate(omega, chi);
        return new EducationalDepthHydraulicResult(
            profile.CrossSectionNumber,
            profile.Side,
            educationalWaterDepth,
            omega,
            chi,
            radius,
            points.Count);
    }

    private static double SegmentLength(double dx, double dh)
    {
        var scale = Math.Max(dx, dh);
        if (scale == 0)
            return 0;

        var scaledDx = dx / scale;
        var scaledDh = dh / scale;
        return scale * Math.Sqrt(scaledDx * scaledDx + scaledDh * scaledDh);
    }
}
