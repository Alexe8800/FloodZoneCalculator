using System;
using System.Collections.Generic;
using System.Linq;

namespace FloodZoneCalculator.Presentation.Visualization;

public sealed class VisualizationProfilePoint
{
    public VisualizationProfilePoint(
        double x,
        double z,
        string side,
        string pointType,
        int pointNumber)
    {
        X = x;
        Z = z;
        Side = side ?? throw new ArgumentNullException(nameof(side));
        PointType = pointType ?? throw new ArgumentNullException(nameof(pointType));
        PointNumber = pointNumber;
    }

    public double X { get; }
    public double Z { get; }
    public string Side { get; }
    public string PointType { get; }
    public int PointNumber { get; }
}

public sealed class VisualizationProfileSegment
{
    public VisualizationProfileSegment(
        VisualizationProfilePoint start,
        VisualizationProfilePoint end)
    {
        Start = start ?? throw new ArgumentNullException(nameof(start));
        End = end ?? throw new ArgumentNullException(nameof(end));
    }

    public VisualizationProfilePoint Start { get; }
    public VisualizationProfilePoint End { get; }
}

public sealed class CrossSectionVisualizationModel
{
    public CrossSectionVisualizationModel(
        int crossSectionNumber,
        double distanceFromHydroUnitM,
        double waterLevel,
        IEnumerable<VisualizationProfilePoint> profilePoints,
        IEnumerable<VisualizationProfileSegment> segments,
        double area,
        double geometricPerimeterBelowWaterLevel,
        double omega,
        double chi,
        double hydraulicRadius,
        double hydraulicSlope,
        double shearVelocity,
        double chezyCoefficient,
        double velocity,
        double discharge)
    {
        CrossSectionNumber = crossSectionNumber;
        DistanceFromHydroUnitM = distanceFromHydroUnitM;
        WaterLevel = waterLevel;
        ProfilePoints = ReadOnly(profilePoints, nameof(profilePoints));
        Segments = ReadOnly(segments, nameof(segments));
        Area = area;
        GeometricPerimeterBelowWaterLevel = geometricPerimeterBelowWaterLevel;
        Omega = omega;
        Chi = chi;
        HydraulicRadius = hydraulicRadius;
        HydraulicSlope = hydraulicSlope;
        ShearVelocity = shearVelocity;
        ChezyCoefficient = chezyCoefficient;
        Velocity = velocity;
        Discharge = discharge;
    }

    public int CrossSectionNumber { get; }
    public double DistanceFromHydroUnitM { get; }
    public double WaterLevel { get; }
    public IReadOnlyList<VisualizationProfilePoint> ProfilePoints { get; }
    public IReadOnlyList<VisualizationProfileSegment> Segments { get; }
    public double Area { get; }
    public double GeometricPerimeterBelowWaterLevel { get; }
    public double Omega { get; }
    public double Chi { get; }
    public double HydraulicRadius { get; }
    public double HydraulicSlope { get; }
    public double ShearVelocity { get; }
    public double ChezyCoefficient { get; }
    public double Velocity { get; }
    public double Discharge { get; }

    private static IReadOnlyList<T> ReadOnly<T>(
        IEnumerable<T> values,
        string parameterName)
    {
        if (values == null)
            throw new ArgumentNullException(parameterName);

        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed class LongitudinalVisualizationSection
{
    public LongitudinalVisualizationSection(
        int number,
        double distanceFromHydroUnitM,
        double bottomLevelZb)
    {
        Number = number;
        DistanceFromHydroUnitM = distanceFromHydroUnitM;
        BottomLevelZb = bottomLevelZb;
    }

    public int Number { get; }
    public double DistanceFromHydroUnitM { get; }
    public double BottomLevelZb { get; }
}

public sealed class LongitudinalVisualizationSegment
{
    public LongitudinalVisualizationSegment(
        int firstNumber,
        int secondNumber,
        double deltaDistanceM,
        double bottomLevelDifferenceM,
        double neutralGeometryRatio)
    {
        FirstNumber = firstNumber;
        SecondNumber = secondNumber;
        DeltaDistanceM = deltaDistanceM;
        BottomLevelDifferenceM = bottomLevelDifferenceM;
        NeutralGeometryRatio = neutralGeometryRatio;
    }

    public int FirstNumber { get; }
    public int SecondNumber { get; }
    public double DeltaDistanceM { get; }
    public double BottomLevelDifferenceM { get; }
    public double NeutralGeometryRatio { get; }
}

public sealed class LongitudinalVisualizationModel
{
    public LongitudinalVisualizationModel(
        IEnumerable<LongitudinalVisualizationSection> sections,
        IEnumerable<LongitudinalVisualizationSegment> segments)
    {
        Sections = ReadOnly(sections, nameof(sections));
        Segments = ReadOnly(segments, nameof(segments));
    }

    public IReadOnlyList<LongitudinalVisualizationSection> Sections { get; }
    public IReadOnlyList<LongitudinalVisualizationSegment> Segments { get; }

    private static IReadOnlyList<T> ReadOnly<T>(
        IEnumerable<T> values,
        string parameterName)
    {
        if (values == null)
            throw new ArgumentNullException(parameterName);

        return Array.AsReadOnly(values.ToArray());
    }
}

public sealed class HydraulicVisualizationModel
{
    public HydraulicVisualizationModel(
        IEnumerable<CrossSectionVisualizationModel> crossSections,
        LongitudinalVisualizationModel longitudinal)
    {
        if (crossSections == null)
            throw new ArgumentNullException(nameof(crossSections));

        CrossSections = Array.AsReadOnly(crossSections.ToArray());
        Longitudinal = longitudinal ?? throw new ArgumentNullException(nameof(longitudinal));
    }

    public IReadOnlyList<CrossSectionVisualizationModel> CrossSections { get; }
    public LongitudinalVisualizationModel Longitudinal { get; }
}
