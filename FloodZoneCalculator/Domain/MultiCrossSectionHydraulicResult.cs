using System;
using System.Collections.Generic;
using System.Linq;

namespace FloodZoneCalculator.Domain;

public sealed class MultiCrossSectionHydraulicResult
{
    public MultiCrossSectionHydraulicResult(
        IReadOnlyList<CrossSectionHydraulicResultItem> sections)
    {
        if (sections == null)
            throw new ArgumentNullException(nameof(sections));

        Sections = Array.AsReadOnly(sections.ToArray());
    }

    public IReadOnlyList<CrossSectionHydraulicResultItem> Sections { get; }
}

public sealed class CrossSectionHydraulicResultItem
{
    public CrossSectionHydraulicResultItem(
        int number,
        double distanceFromHydroUnitM,
        CrossSectionHydraulicResult hydraulicResult)
    {
        if (hydraulicResult == null)
            throw new ArgumentNullException(nameof(hydraulicResult));

        if (double.IsNaN(distanceFromHydroUnitM) ||
            double.IsInfinity(distanceFromHydroUnitM))
        {
            throw new ArgumentOutOfRangeException(
                nameof(distanceFromHydroUnitM),
                distanceFromHydroUnitM,
                "DistanceFromHydroUnitM должен быть конечным числом.");
        }

        if (distanceFromHydroUnitM < 0)
            throw new ArgumentOutOfRangeException(
                nameof(distanceFromHydroUnitM),
                distanceFromHydroUnitM,
                "DistanceFromHydroUnitM не может быть отрицательным.");

        Number = number;
        DistanceFromHydroUnitM = distanceFromHydroUnitM;
        HydraulicResult = hydraulicResult;
    }

    public int Number { get; }
    public double DistanceFromHydroUnitM { get; }
    public CrossSectionHydraulicResult HydraulicResult { get; }
}
