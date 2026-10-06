namespace FloodZoneCalculator.Domain;

public sealed class CrossSectionGeometryCalculationResult
{
    public CrossSectionGeometryCalculationResult(
        double waterLevel,
        double area,
        double geometricPerimeterBelowWaterLevel)
    {
        WaterLevel = waterLevel;
        Area = area;
        GeometricPerimeterBelowWaterLevel = geometricPerimeterBelowWaterLevel;
    }

    public double WaterLevel { get; }
    public double Area { get; }
    public double GeometricPerimeterBelowWaterLevel { get; }
}
