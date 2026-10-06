using FloodZoneDb.Client;

namespace FloodZoneCalculator.Domain;

public sealed class EducationalDepthHydraulicResult
{
    public int CrossSectionNumber { get; }
    public IsodatSide Side { get; }
    public double EducationalWaterDepth { get; }
    public double Omega { get; }
    public double Chi { get; }
    public double EducationalHydraulicRadius { get; }
    public int PointCount { get; }
    public string Interpretation => IsodatHydraulicPoint.EducationalSyntheticInterpretation;

    internal EducationalDepthHydraulicResult(
        int crossSectionNumber,
        IsodatSide side,
        double educationalWaterDepth,
        double omega,
        double chi,
        double educationalHydraulicRadius,
        int pointCount)
    {
        CrossSectionNumber = crossSectionNumber;
        Side = side;
        EducationalWaterDepth = educationalWaterDepth;
        Omega = omega;
        Chi = chi;
        EducationalHydraulicRadius = educationalHydraulicRadius;
        PointCount = pointCount;
    }
}
