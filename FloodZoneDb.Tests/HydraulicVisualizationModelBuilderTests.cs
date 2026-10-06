using FloodZoneCalculator.Domain;
using FloodZoneCalculator.Presentation.Visualization;
using FloodZoneDb.Client;
using System.Globalization;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class HydraulicVisualizationModelBuilderTests
{
    [Fact]
    public void Build_CopiesAllFiveProfilesAndCalculatedValuesWithoutRecalculation()
    {
        var records = CreateRecords();
        var prepared = Prepare(records);

        var model = Build(prepared);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 },
            model.CrossSections.Select(section => section.CrossSectionNumber));
        foreach (var item in prepared)
        {
            var visualization = model.CrossSections.Single(
                section => section.CrossSectionNumber == item.Record.Number);

            Assert.Equal(item.Geometry.Points.Count, visualization.ProfilePoints.Count);
            Assert.Equal(item.Geometry.Segments.Count, visualization.Segments.Count);
            Assert.Equal(item.Geometry.DistanceFromHydroUnitM,
                visualization.DistanceFromHydroUnitM);
            Assert.Equal(item.Result.WaterLevel, visualization.WaterLevel);
            Assert.Equal(item.GeometryResult.Area, visualization.Area);
            Assert.Equal(item.GeometryResult.GeometricPerimeterBelowWaterLevel,
                visualization.GeometricPerimeterBelowWaterLevel);
            Assert.Equal(item.Result.Omega, visualization.Omega);
            Assert.Equal(item.Result.Chi, visualization.Chi);
            Assert.Equal(visualization.Area, visualization.Omega);
            Assert.Equal(
                visualization.GeometricPerimeterBelowWaterLevel,
                visualization.Chi);
            Assert.Equal(item.Result.HydraulicRadius, visualization.HydraulicRadius);
            Assert.Equal(item.Result.ChezyCoefficient, visualization.ChezyCoefficient);
            Assert.Equal(item.Result.Velocity, visualization.Velocity);
            Assert.Equal(item.Result.Discharge, visualization.Discharge);

            Assert.Equal(
                item.Geometry.Points.Select(point => (point.DistanceM, point.ElevationM,
                    point.Side, point.PointType, point.PointNumber)),
                visualization.ProfilePoints.Select(point => (point.X, point.Z,
                    point.Side, point.PointType, point.PointNumber)));
        }

        Assert.Equal(5, model.Longitudinal.Sections.Count);
        Assert.Equal(4, model.Longitudinal.Segments.Count);
        Assert.Equal(5, model.CrossSections[2].ProfilePoints.Count);
    }

    [Fact]
    public void Build_UsesDistanceForSpatialOrderAndPreservesNeutralGeometryRatio()
    {
        var records = CreateRecords();
        records.Single(record => record.Number == 2).DistanceFromHydroUnitM = "6000";
        var prepared = Prepare(records);

        var model = Build(prepared);

        Assert.Equal(new[] { 1, 3, 4, 2, 5 },
            model.Longitudinal.Sections.Select(section => section.Number));
        Assert.Equal(new[] { 1, 3, 4, 2 },
            model.Longitudinal.Segments.Select(segment => segment.FirstNumber));
        Assert.Equal(new[] { 3, 4, 2, 5 },
            model.Longitudinal.Segments.Select(segment => segment.SecondNumber));
        Assert.Equal(2500, model.Longitudinal.Segments[0].DeltaDistanceM);
        Assert.Equal(1500, model.Longitudinal.Segments[1].DeltaDistanceM);
        Assert.Equal(2000, model.Longitudinal.Segments[2].DeltaDistanceM);
        Assert.Equal(1000, model.Longitudinal.Segments[3].DeltaDistanceM);
        Assert.Equal(-1.0 / 2500,
            model.Longitudinal.Segments[0].NeutralGeometryRatio);
        Assert.Equal(1.0 / 1500,
            model.Longitudinal.Segments[1].NeutralGeometryRatio);
        Assert.Equal(-1.0 / 2000,
            model.Longitudinal.Segments[2].NeutralGeometryRatio);
    }

    [Fact]
    public void Build_DoesNotChangeExistingObjectsOrHydraulicResults()
    {
        var records = CreateRecords();
        var prepared = Prepare(records);
        var pointsBefore = records.ToDictionary(
            record => record.Number,
            record => record.BankPoints.Select(point => point.DistanceM).ToArray());
        var resultBefore = prepared.ToDictionary(
            item => item.Record.Number,
            item => item.Result);

        var first = Build(prepared);
        var second = Build(prepared);

        foreach (var record in records)
        {
            Assert.Equal(pointsBefore[record.Number],
                record.BankPoints.Select(point => point.DistanceM));
        }

        foreach (var item in prepared)
        {
            Assert.Same(resultBefore[item.Record.Number], item.Result);
        }

        Assert.Equal(
            first.CrossSections.Select(section => section.Discharge),
            second.CrossSections.Select(section => section.Discharge));
        Assert.Equal(
            first.Longitudinal.Segments.Select(segment => segment.NeutralGeometryRatio),
            second.Longitudinal.Segments.Select(segment => segment.NeutralGeometryRatio));
    }

    [Fact]
    public void Build_UsesReadyHydraulicValuesInsteadOfDerivingThemFromGeometry()
    {
        var prepared = Prepare(CreateRecords());
        var original = prepared[0].Result;
        var supplied = new CrossSectionHydraulicResult(
            original.WaterLevel,
            101,
            102,
            103,
            104,
            105,
            106,
            107,
            108);
        prepared[0] = prepared[0] with { Result = supplied };

        var model = Build(prepared);
        var visualization = model.CrossSections.Single(section =>
            section.CrossSectionNumber == prepared[0].Record.Number);

        Assert.Equal(101, visualization.Omega);
        Assert.Equal(102, visualization.Chi);
        Assert.Equal(103, visualization.HydraulicRadius);
        Assert.Equal(106, visualization.ChezyCoefficient);
        Assert.Equal(107, visualization.Velocity);
        Assert.Equal(108, visualization.Discharge);
    }

    private static HydraulicVisualizationModel Build(
        List<PreparedSection> prepared)
    {
        var sequence = new CrossSectionSequence(
            prepared.Select(item => item.Record));
        var pairs = new CrossSectionAdjacentPairBuilder().Build(sequence);
        var recordsByNumber = prepared.ToDictionary(item => item.Record.Number);
        var longitudinal = pairs.Select(pair =>
            new CrossSectionLongitudinalGeometryBuilder().Build(
                pair,
                recordsByNumber[pair.First.Number].Record,
                recordsByNumber[pair.Second.Number].Record));

        var results = new MultiCrossSectionHydraulicResult(
            prepared.Select(item => new CrossSectionHydraulicResultItem(
                item.Record.Number,
                item.Geometry.DistanceFromHydroUnitM,
                item.Result)).ToArray());

        return new HydraulicVisualizationModelBuilder().Build(
            prepared.Select(item => new CrossSectionVisualizationSource(
                item.Geometry,
                item.GeometryResult)),
            results,
            sequence,
            longitudinal);
    }

    private static List<PreparedSection> Prepare(
        IReadOnlyList<CrossSectionRecord> records)
    {
        var geometryBuilder = new CrossSectionGeometryBuilder();
        var hydraulicCalculator = new CrossSectionHydraulicResultCalculator();
        var input = new HydraulicCalculationInput(106, 9.81, 0.000001, 2, 20, 0.001);

        return records.Select(record =>
        {
            var geometry = geometryBuilder.Build(record);
            var geometryResult = new CrossSectionGeometryCalculator().Calculate(
                geometry,
                input.WaterLevel);
            var result = hydraulicCalculator.Calculate(geometry, input);
            return new PreparedSection(record, geometry, geometryResult, result);
        }).ToList();
    }

    private static List<CrossSectionRecord> CreateRecords()
    {
        var records = new[]
        {
            TestCrossSectionFactory.Symmetric(),
            TestCrossSectionFactory.Asymmetric(),
            TestCrossSectionFactory.Incomplete(),
            TestCrossSectionFactory.FlatBottom(),
            TestCrossSectionFactory.SlopedBottom()
        }.ToList();
        var distances = new[] { 0, 1000, 2500, 4000, 7000 };
        var bottoms = new[] { 65, 64, 64, 65, 63 };

        for (var index = 0; index < records.Count; index++)
        {
            records[index].Number = index + 1;
            records[index].DistanceFromHydroUnitM =
                distances[index].ToString(CultureInfo.InvariantCulture);
            records[index].BottomLevelZb =
                bottoms[index].ToString(CultureInfo.InvariantCulture);
        }

        return records;
    }

    private sealed record PreparedSection(
        CrossSectionRecord Record,
        CrossSectionGeometry Geometry,
        CrossSectionGeometryCalculationResult GeometryResult,
        CrossSectionHydraulicResult Result);
}
