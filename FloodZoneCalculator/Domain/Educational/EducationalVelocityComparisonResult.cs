using FloodZoneDb.Client;

namespace FloodZoneCalculator.Domain;

public sealed class EducationalVelocityComparisonResult
{
    public int CrossSectionNumber { get; }
    public IsodatSide Side { get; }
    public int ObservedCount { get; }
    public double ObservedVelocityMin { get; }
    public double ObservedVelocityMax { get; }
    public double ObservedVelocityMean { get; }
    public double CalculatedVelocity { get; }
    public double Difference { get; }
    public double AbsoluteDifference { get; }
    public double? RelativeDifference { get; }
    public double? Ratio { get; }
    public string Interpretation => IsodatHydraulicPoint.EducationalSyntheticInterpretation;

    internal EducationalVelocityComparisonResult(
        int crossSectionNumber,
        IsodatSide side,
        int observedCount,
        double observedVelocityMin,
        double observedVelocityMax,
        double observedVelocityMean,
        double calculatedVelocity,
        double difference,
        double absoluteDifference,
        double? relativeDifference,
        double? ratio)
    {
        CrossSectionNumber = crossSectionNumber;
        Side = side;
        ObservedCount = observedCount;
        ObservedVelocityMin = observedVelocityMin;
        ObservedVelocityMax = observedVelocityMax;
        ObservedVelocityMean = observedVelocityMean;
        CalculatedVelocity = calculatedVelocity;
        Difference = difference;
        AbsoluteDifference = absoluteDifference;
        RelativeDifference = relativeDifference;
        Ratio = ratio;
    }
}
