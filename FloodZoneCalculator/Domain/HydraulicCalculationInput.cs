using System;

namespace FloodZoneCalculator.Domain;

public sealed class HydraulicCalculationInput
{
    public HydraulicCalculationInput(
        double waterLevel,
        double gravity,
        double kinematicViscosity,
        double depthH,
        double widthB,
        double hydraulicSlopeIf)
    {
        ValidateFinite(waterLevel, nameof(waterLevel));
        ValidatePositiveFinite(gravity, nameof(gravity));
        ValidatePositiveFinite(kinematicViscosity, nameof(kinematicViscosity));
        ValidatePositiveFinite(depthH, nameof(depthH));
        ValidatePositiveFinite(widthB, nameof(widthB));
        ValidateFinite(hydraulicSlopeIf, nameof(hydraulicSlopeIf));

        WaterLevel = waterLevel;
        Gravity = gravity;
        KinematicViscosity = kinematicViscosity;
        DepthH = depthH;
        WidthB = widthB;
        HydraulicSlopeIf = hydraulicSlopeIf;
    }

    public double WaterLevel { get; }
    public double Gravity { get; }
    public double KinematicViscosity { get; }
    public double DepthH { get; }
    public double WidthB { get; }
    public double HydraulicSlopeIf { get; }

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        ValidateFinite(value, parameterName);
        if (value <= 0)
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Значение должно быть больше нуля.");
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Значение должно быть конечным числом.");
    }
}
