using System;

namespace FloodZoneCalculator.Domain;

public sealed class LongitudinalGeometryRatioCalculator
{
    public double Calculate(CrossSectionLongitudinalGeometry geometry)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));

        var deltaDistance = geometry.DeltaDistanceM;
        var bottomLevelDifference = geometry.BottomLevelDifferenceM;

        if (double.IsNaN(deltaDistance) || double.IsInfinity(deltaDistance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(geometry),
                deltaDistance,
                "DeltaDistanceM должен быть конечным числом.");
        }

        if (deltaDistance < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(geometry),
                deltaDistance,
                "DeltaDistanceM не может быть отрицательным.");
        }

        if (double.IsNaN(bottomLevelDifference) ||
            double.IsInfinity(bottomLevelDifference))
        {
            throw new ArgumentOutOfRangeException(
                nameof(geometry),
                bottomLevelDifference,
                "BottomLevelDifferenceM должен быть конечным числом.");
        }

        if (deltaDistance == 0)
            throw new DivideByZeroException(
                "Невозможно вычислить отношение при DeltaDistanceM, равном нулю.");

        var ratio = bottomLevelDifference / deltaDistance;
        if (double.IsNaN(ratio) || double.IsInfinity(ratio))
        {
            throw new ArithmeticException(
                "Результат геометрического отношения должен быть конечным числом.");
        }

        return ratio;
    }
}
