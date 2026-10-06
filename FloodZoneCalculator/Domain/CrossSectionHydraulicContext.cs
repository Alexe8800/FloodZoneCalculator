using System;

namespace FloodZoneCalculator.Domain;

public sealed class CrossSectionHydraulicContext
{
    public CrossSectionHydraulicContext(
        CrossSectionGeometry geometry,
        HydraulicCalculationInput input)
    {
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        Input = input ?? throw new ArgumentNullException(nameof(input));
    }

    public CrossSectionGeometry Geometry { get; }
    public HydraulicCalculationInput Input { get; }
}
