using FloodZoneCalculator.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FloodZoneCalculator.Presentation.Visualization;

public sealed class CrossSectionVisualizationSource
{
    public CrossSectionVisualizationSource(
        CrossSectionGeometry geometry,
        CrossSectionGeometryCalculationResult geometryResult)
    {
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        GeometryResult = geometryResult ??
            throw new ArgumentNullException(nameof(geometryResult));
    }

    public CrossSectionGeometry Geometry { get; }
    public CrossSectionGeometryCalculationResult GeometryResult { get; }
}

public sealed class HydraulicVisualizationModelBuilder
{
    public HydraulicVisualizationModel Build(
        IEnumerable<CrossSectionVisualizationSource> sources,
        MultiCrossSectionHydraulicResult hydraulicResults,
        CrossSectionSequence sequence,
        IEnumerable<CrossSectionLongitudinalGeometry> longitudinalGeometries)
    {
        if (sources == null)
            throw new ArgumentNullException(nameof(sources));
        if (hydraulicResults == null)
            throw new ArgumentNullException(nameof(hydraulicResults));
        if (sequence == null)
            throw new ArgumentNullException(nameof(sequence));
        if (longitudinalGeometries == null)
            throw new ArgumentNullException(nameof(longitudinalGeometries));

        var sourceByNumber = sources.ToDictionary(
            source => source.Geometry.Number,
            source => source);
        var resultByNumber = hydraulicResults.Sections.ToDictionary(
            result => result.Number,
            result => result.HydraulicResult);

        var crossSections = sequence.Items
            .Select(item => BuildCrossSection(
                Require(sourceByNumber, item.Number, nameof(sources)),
                Require(resultByNumber, item.Number, nameof(hydraulicResults))))
            .ToArray();

        var longitudinal = BuildLongitudinal(
            sequence,
            longitudinalGeometries,
            new LongitudinalGeometryRatioCalculator());

        return new HydraulicVisualizationModel(crossSections, longitudinal);
    }

    private static CrossSectionVisualizationModel BuildCrossSection(
        CrossSectionVisualizationSource source,
        CrossSectionHydraulicResult hydraulicResult)
    {
        var points = source.Geometry.Points
            .Select(point => new VisualizationProfilePoint(
                point.DistanceM,
                point.ElevationM,
                point.Side,
                point.PointType,
                point.PointNumber))
            .ToArray();

        var segments = source.Geometry.Segments
            .Select(segment => new VisualizationProfileSegment(
                FindPoint(points, segment.Start),
                FindPoint(points, segment.End)))
            .ToArray();

        return new CrossSectionVisualizationModel(
            source.Geometry.Number,
            source.Geometry.DistanceFromHydroUnitM,
            hydraulicResult.WaterLevel,
            points,
            segments,
            source.GeometryResult.Area,
            source.GeometryResult.GeometricPerimeterBelowWaterLevel,
            hydraulicResult.Omega,
            hydraulicResult.Chi,
            hydraulicResult.HydraulicRadius,
            hydraulicResult.HydraulicSlope,
            hydraulicResult.ShearVelocity,
            hydraulicResult.ChezyCoefficient,
            hydraulicResult.Velocity,
            hydraulicResult.Discharge);
    }

    private static LongitudinalVisualizationModel BuildLongitudinal(
        CrossSectionSequence sequence,
        IEnumerable<CrossSectionLongitudinalGeometry> geometries,
        LongitudinalGeometryRatioCalculator ratioCalculator)
    {
        var sections = sequence.Items
            .Select(item => new LongitudinalVisualizationSection(
                item.Number,
                item.DistanceFromHydroUnitM,
                FindBottomLevel(item.Number, geometries)))
            .ToArray();

        var geometryList = geometries.ToArray();
        var segments = sequence.Items
            .Zip(sequence.Items.Skip(1), (first, second) =>
                FindGeometry(geometryList, first.Number, second.Number))
            .Select(geometry => new LongitudinalVisualizationSegment(
                geometry.Pair.First.Number,
                geometry.Pair.Second.Number,
                geometry.DeltaDistanceM,
                geometry.BottomLevelDifferenceM,
                ratioCalculator.Calculate(geometry)))
            .ToArray();

        return new LongitudinalVisualizationModel(sections, segments);
    }

    private static VisualizationProfilePoint FindPoint(
        IReadOnlyList<VisualizationProfilePoint> points,
        ProfilePoint source)
    {
        return points.First(point =>
            point.PointNumber == source.PointNumber &&
            string.Equals(point.Side, source.Side, StringComparison.Ordinal) &&
            string.Equals(point.PointType, source.PointType, StringComparison.Ordinal) &&
            point.X == source.DistanceM &&
            point.Z == source.ElevationM);
    }

    private static CrossSectionLongitudinalGeometry FindGeometry(
        IReadOnlyList<CrossSectionLongitudinalGeometry> geometries,
        int firstNumber,
        int secondNumber)
    {
        var geometry = geometries.FirstOrDefault(candidate =>
            candidate.Pair.First.Number == firstNumber &&
            candidate.Pair.Second.Number == secondNumber);
        if (geometry == null)
        {
            throw new ArgumentException(
                "Для соседней пары отсутствует продольная геометрия.",
                nameof(geometries));
        }

        return geometry;
    }

    private static double FindBottomLevel(
        int number,
        IEnumerable<CrossSectionLongitudinalGeometry> geometries)
    {
        foreach (var geometry in geometries)
        {
            if (geometry.Pair.First.Number == number)
                return geometry.FirstBottomLevelZb;
            if (geometry.Pair.Second.Number == number)
                return geometry.SecondBottomLevelZb;
        }

        throw new ArgumentException(
            "Для створа отсутствует продольная геометрия.",
            nameof(geometries));
    }

    private static T Require<T>(
        IReadOnlyDictionary<int, T> values,
        int number,
        string parameterName)
    {
        if (!values.TryGetValue(number, out var value))
        {
            throw new ArgumentException(
                "Для створа отсутствуют данные визуализации.",
                parameterName);
        }

        return value;
    }
}
