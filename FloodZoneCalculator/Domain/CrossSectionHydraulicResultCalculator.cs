using System;

namespace FloodZoneCalculator.Domain;

public sealed class CrossSectionHydraulicResultCalculator
{
    private readonly CrossSectionGeometryCalculator geometryCalculator = new();
    private readonly HydraulicRadiusCalculator hydraulicRadiusCalculator = new();
    private readonly HydraulicSlopeValidator hydraulicSlopeValidator = new();
    private readonly ShearVelocityCalculator shearVelocityCalculator = new();
    private readonly DischargeCalculator dischargeCalculator = new();

    public CrossSectionHydraulicResult Calculate(
        CrossSectionHydraulicContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        return Calculate(context.Geometry, context.Input);
    }

    public CrossSectionHydraulicResult Calculate(
        CrossSectionGeometry geometry,
        HydraulicCalculationInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        return Calculate(
            geometry,
            input.WaterLevel,
            input.Gravity,
            input.KinematicViscosity,
            input.DepthH,
            input.WidthB,
            input.HydraulicSlopeIf);
    }

    public CrossSectionHydraulicResult Calculate(
        CrossSectionGeometry geometry,
        double waterLevel,
        double gravity,
        double kinematicViscosity,
        double depth,
        double width,
        double hydraulicSlope)
    {
        var geometryResult = geometryCalculator.Calculate(geometry, waterLevel);
        var omega = geometryResult.Area;
        var chi = geometryResult.GeometricPerimeterBelowWaterLevel;
        var hydraulicRadius = hydraulicRadiusCalculator.Calculate(omega, chi);

        hydraulicSlopeValidator.Validate(hydraulicSlope);

        var shearVelocity = shearVelocityCalculator.Calculate(
            hydraulicRadius,
            hydraulicSlope,
            gravity);
        var chezyCoefficient = GrishaninChezyCalculator.Calculate(
            gravity,
            kinematicViscosity,
            depth,
            width);
        var velocity = ChezyVelocityCalculator.Calculate(
            chezyCoefficient,
            hydraulicRadius,
            hydraulicSlope);
        var discharge = dischargeCalculator.Calculate(velocity, omega);

        return new CrossSectionHydraulicResult(
            waterLevel,
            omega,
            chi,
            hydraulicRadius,
            hydraulicSlope,
            shearVelocity,
            chezyCoefficient,
            velocity,
            discharge);
    }
}
