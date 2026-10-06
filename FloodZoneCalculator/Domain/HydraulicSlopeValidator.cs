using System;

namespace FloodZoneCalculator.Domain;

public sealed class HydraulicSlopeValidator
{
    public void Validate(double hydraulicSlope)
    {
        if (double.IsNaN(hydraulicSlope) || double.IsInfinity(hydraulicSlope))
            throw new ArgumentOutOfRangeException(
                nameof(hydraulicSlope),
                hydraulicSlope,
                "Гидравлический уклон If должен быть конечным числом.");
    }
}
