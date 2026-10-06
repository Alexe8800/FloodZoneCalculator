using FloodZoneDb.Client;

namespace FloodZoneCalculator.Domain;

public sealed class EducationalFullHydraulicResult
{
    public int CrossSectionNumber { get; }
    public IsodatSide Side { get; }
    public int PointCount { get; }
    public double MinX { get; }
    public double MaxX { get; }
    public double B { get; }
    public double Omega { get; }
    public double Chi { get; }
    public double HydraulicRadius { get; }
    public double Gravity { get; }
    public double KinematicViscosity { get; }
    public double EducationalWaterDepth { get; }
    public double HydraulicSlope { get; }
    public double ShearVelocity { get; }
    public double ChezyCoefficient { get; }
    public double CalculatedVelocity { get; }
    public double Discharge { get; }
    public double ObservedVelocityMin { get; }
    public double ObservedVelocityMax { get; }
    public string Interpretation => IsodatHydraulicPoint.EducationalSyntheticInterpretation;

    internal EducationalFullHydraulicResult(
        int crossSectionNumber,
        IsodatSide side,
        int pointCount,
        double minX,
        double maxX,
        double width,
        EducationalDepthHydraulicResult geometry,
        double gravity,
        double kinematicViscosity,
        double educationalWaterDepth,
        double hydraulicSlope,
        double shearVelocity,
        double chezyCoefficient,
        double calculatedVelocity,
        double discharge,
        double observedVelocityMin,
        double observedVelocityMax)
    {
        CrossSectionNumber = crossSectionNumber;
        Side = side;
        PointCount = pointCount;
        MinX = minX;
        MaxX = maxX;
        B = width;
        Omega = geometry.Omega;
        Chi = geometry.Chi;
        HydraulicRadius = geometry.EducationalHydraulicRadius;
        Gravity = gravity;
        KinematicViscosity = kinematicViscosity;
        EducationalWaterDepth = educationalWaterDepth;
        HydraulicSlope = hydraulicSlope;
        ShearVelocity = shearVelocity;
        ChezyCoefficient = chezyCoefficient;
        CalculatedVelocity = calculatedVelocity;
        Discharge = discharge;
        ObservedVelocityMin = observedVelocityMin;
        ObservedVelocityMax = observedVelocityMax;
    }
}
