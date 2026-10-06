namespace FloodZoneCalculator.Domain;

public sealed class CrossSectionHydraulicResult
{
    public CrossSectionHydraulicResult(
        double waterLevel,
        double omega,
        double chi,
        double hydraulicRadius,
        double hydraulicSlope,
        double shearVelocity,
        double chezyCoefficient,
        double velocity,
        double discharge)
    {
        WaterLevel = waterLevel;
        Omega = omega;
        Chi = chi;
        HydraulicRadius = hydraulicRadius;
        HydraulicSlope = hydraulicSlope;
        ShearVelocity = shearVelocity;
        ChezyCoefficient = chezyCoefficient;
        Velocity = velocity;
        Discharge = discharge;
    }

    public double WaterLevel { get; }
    public double Omega { get; }
    public double Chi { get; }
    public double HydraulicRadius { get; }
    public double HydraulicSlope { get; }
    public double ShearVelocity { get; }
    public double ChezyCoefficient { get; }
    public double Velocity { get; }
    public double Discharge { get; }
}
