using System;

namespace FloodZoneCalculator.Domain;

public sealed class AdjacentCrossSectionPair
{
    public AdjacentCrossSectionPair(
        CrossSectionSequenceItem first,
        CrossSectionSequenceItem second)
    {
        First = first ?? throw new ArgumentNullException(nameof(first));
        Second = second ?? throw new ArgumentNullException(nameof(second));

        if (second.DistanceFromHydroUnitM < first.DistanceFromHydroUnitM)
        {
            throw new ArgumentException(
                "Второй створ не может находиться раньше первого.",
                nameof(second));
        }
    }

    public CrossSectionSequenceItem First { get; }
    public CrossSectionSequenceItem Second { get; }

    public double DistanceDeltaM =>
        Second.DistanceFromHydroUnitM - First.DistanceFromHydroUnitM;
}
