using System;

namespace FloodZoneCalculator.Domain;

public sealed class CrossSectionLongitudinalGeometry
{
    public CrossSectionLongitudinalGeometry(
        AdjacentCrossSectionPair pair,
        double firstBottomLevelZb,
        double secondBottomLevelZb)
    {
        Pair = pair ?? throw new ArgumentNullException(nameof(pair));

        ValidateFinite(firstBottomLevelZb, nameof(firstBottomLevelZb));
        ValidateFinite(secondBottomLevelZb, nameof(secondBottomLevelZb));

        DeltaDistanceM = pair.DistanceDeltaM;
        FirstBottomLevelZb = firstBottomLevelZb;
        SecondBottomLevelZb = secondBottomLevelZb;
        BottomLevelDifferenceM = secondBottomLevelZb - firstBottomLevelZb;
    }

    public AdjacentCrossSectionPair Pair { get; }
    public double DeltaDistanceM { get; }
    public double FirstBottomLevelZb { get; }
    public double SecondBottomLevelZb { get; }
    public double BottomLevelDifferenceM { get; }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Отметка дна должна быть конечным числом.");
        }
    }
}
