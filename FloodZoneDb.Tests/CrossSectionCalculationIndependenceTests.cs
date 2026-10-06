using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class CrossSectionCalculationIndependenceTests
{
    private static HydraulicCalculationInput InputA() =>
        new(106, 9.81, 1.15e-6, 5, 50, 0.001);

    private static HydraulicCalculationInput InputB() =>
        new(105, 9.81, 1.15e-6, 5, 50, 0.001);

    [Fact]
    public void Multi_section_calculation_does_not_mutate_records_geometry_or_bank_points()
    {
        var sections = CreateSections();
        var recordSnapshots = sections.ToDictionary(section => section.Number, Snapshot);
        var geometries = sections.ToDictionary(
            section => section.Number,
            section => Build(section));
        var geometrySnapshots = geometries.ToDictionary(
            pair => pair.Key,
            pair => Snapshot(pair.Value));
        var input = InputA();
        var inputSnapshot = Snapshot(input);

        _ = Calculate(sections, input);

        Assert.Equal(new[] { 4, 1, 5, 3, 2 }, sections.Select(section => section.Number));
        foreach (var section in sections)
        {
            Assert.Equal(recordSnapshots[section.Number], Snapshot(section));
            Assert.Equal(geometrySnapshots[section.Number], Snapshot(geometries[section.Number]));
            Assert.Equal(geometrySnapshots[section.Number], Snapshot(Build(section)));
        }
        Assert.Equal(inputSnapshot, Snapshot(input));
        Assert.Equal(5, geometries[3].Points.Count);
        Assert.Equal(4, geometries[3].Segments.Count);
    }

    [Fact]
    public void Changing_only_profile_02_does_not_change_other_profile_results()
    {
        var baselineSections = CreateSections();
        var changedSections = CloneSections(baselineSections);
        var profile02 = changedSections.Single(section => section.Number == 2);
        var changedPoint = profile02.BankPoints.Single(point => point.PointNumber == 3);
        changedPoint.ElevationM = "102";

        var baseline = Calculate(baselineSections, InputA());
        var changed = Calculate(changedSections, InputA());

        foreach (var number in new[] { 1, 3, 4, 5 })
            AssertResultsEqual(ResultFor(baseline, number), ResultFor(changed, number));

        AssertResultsNotEqual(ResultFor(baseline, 2), ResultFor(changed, 2));
        Assert.Equal(9, profile02.BankPoints.Count);
        Assert.Equal(
            Snapshot(baselineSections.Single(section => section.Number == 1)),
            Snapshot(changedSections.Single(section => section.Number == 1)));
        Assert.Equal(
            Snapshot(baselineSections.Single(section => section.Number == 3)),
            Snapshot(changedSections.Single(section => section.Number == 3)));
    }

    [Fact]
    public void Separate_inputs_produce_independent_results_without_mutating_inputs_or_geometry()
    {
        var sections = CreateSections();
        var recordSnapshots = sections.ToDictionary(section => section.Number, Snapshot);
        var geometrySnapshots = sections.ToDictionary(
            section => section.Number,
            section => Snapshot(Build(section)));
        var inputA = InputA();
        var inputB = InputB();
        var inputASnapshot = Snapshot(inputA);
        var inputBSnapshot = Snapshot(inputB);

        var resultA = Calculate(sections, inputA);
        var resultB = Calculate(sections, inputB);

        foreach (var section in sections)
        {
            var geometry = Build(section);
            Assert.Equal(recordSnapshots[section.Number], Snapshot(section));
            Assert.Equal(geometrySnapshots[section.Number], Snapshot(geometry));
            AssertResultsEqual(
                SingleResult(section, inputA),
                ResultFor(resultA, section.Number));
            AssertResultsEqual(
                SingleResult(section, inputB),
                ResultFor(resultB, section.Number));
        }
        Assert.Equal(inputASnapshot, Snapshot(inputA));
        Assert.Equal(inputBSnapshot, Snapshot(inputB));
        Assert.NotEqual(
            ResultFor(resultA, 1).Omega,
            ResultFor(resultB, 1).Omega);
    }

    [Fact]
    public void Input_order_does_not_change_physical_results()
    {
        var orderA = CreateSections();
        var orderB = new[]
        {
            orderA.Single(section => section.Number == 5),
            orderA.Single(section => section.Number == 3),
            orderA.Single(section => section.Number == 1),
            orderA.Single(section => section.Number == 4),
            orderA.Single(section => section.Number == 2)
        };

        var resultA = Calculate(orderA, InputA());
        var resultB = Calculate(orderB, InputA());

        foreach (var number in Enumerable.Range(1, 5))
            AssertResultsEqual(ResultFor(resultA, number), ResultFor(resultB, number));
    }

    [Fact]
    public void Changing_distances_changes_summary_order_only()
    {
        var setA = CreateSections(new[] { 0d, 1000d, 2500d, 4000d, 7000d });
        var setB = CreateSections(new[] { 0d, 500d, 3000d, 8000d, 15000d });

        var resultA = Calculate(setA, InputA());
        var resultB = Calculate(setB, InputA());

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, resultA.Sections.Select(item => item.Number));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, resultB.Sections.Select(item => item.Number));
        foreach (var number in Enumerable.Range(1, 5))
            AssertResultsEqual(ResultFor(resultA, number), ResultFor(resultB, number));
        Assert.NotEqual(
            resultA.Sections.Select(item => item.DistanceFromHydroUnitM),
            resultB.Sections.Select(item => item.DistanceFromHydroUnitM));
    }

    [Fact]
    public void Three_repeated_calculations_match_field_by_field()
    {
        var sections = CreateSections();
        var calculator = new MultiCrossSectionHydraulicResultCalculator();
        var first = calculator.Calculate(sections, InputA());
        var second = calculator.Calculate(sections, InputA());
        var third = calculator.Calculate(sections, InputA());

        foreach (var number in Enumerable.Range(1, 5))
        {
            AssertResultsEqual(ResultFor(first, number), ResultFor(second, number));
            AssertResultsEqual(ResultFor(first, number), ResultFor(third, number));
        }
    }

    [Fact]
    public void Multi_section_matches_five_independent_context_calculations()
    {
        var sections = CreateSections();
        var input = InputA();
        var summary = Calculate(sections, input);

        foreach (var section in sections)
            AssertResultsEqual(
                SingleResult(section, input),
                ResultFor(summary, section.Number));

        var incomplete = Build(sections.Single(section => section.Number == 3));
        Assert.Equal(5, incomplete.Points.Count);
        Assert.Equal(4, incomplete.Segments.Count);
    }

    [Fact]
    public void Existing_result_does_not_change_when_source_record_is_modified_after_calculation()
    {
        var sections = CreateSections();
        var result = Calculate(sections, InputA());
        var saved = ResultFor(result, 2);
        var source = sections.Single(section => section.Number == 2);

        source.BankPoints.Single(point => point.PointNumber == 3).ElevationM = "90";
        source.DistanceFromHydroUnitM = "99999";

        AssertResultsEqual(saved, ResultFor(result, 2));
    }

    private static MultiCrossSectionHydraulicResult Calculate(
        IEnumerable<CrossSectionRecord> sections,
        HydraulicCalculationInput input) =>
        new MultiCrossSectionHydraulicResultCalculator().Calculate(sections, input);

    private static CrossSectionHydraulicResult SingleResult(
        CrossSectionRecord section,
        HydraulicCalculationInput input)
    {
        var geometry = Build(section);
        var context = new CrossSectionHydraulicContext(geometry, input);
        return new CrossSectionHydraulicResultCalculator().Calculate(context);
    }

    private static CrossSectionGeometry Build(CrossSectionRecord section) =>
        new CrossSectionGeometryBuilder().Build(section);

    private static CrossSectionRecord[] CreateSections(
        double[]? distances = null)
    {
        distances ??= new[] { 0d, 1000d, 2500d, 4000d, 7000d };
        return new[]
        {
            Configure(TestCrossSectionFactory.FlatBottom(), 4, distances[3]),
            Configure(TestCrossSectionFactory.Symmetric(), 1, distances[0]),
            Configure(TestCrossSectionFactory.SlopedBottom(), 5, distances[4]),
            Configure(TestCrossSectionFactory.Incomplete(), 3, distances[2]),
            Configure(TestCrossSectionFactory.Asymmetric(), 2, distances[1])
        };
    }

    private static CrossSectionRecord[] CloneSections(
        IEnumerable<CrossSectionRecord> sections) =>
        sections.Select(section => new CrossSectionRecord
        {
            Id = section.Id,
            ObjectId = section.ObjectId,
            Number = section.Number,
            DistanceFromHydroUnitM = section.DistanceFromHydroUnitM,
            BottomLevelZb = section.BottomLevelZb,
            DepthHb = section.DepthHb,
            WidthBb = section.WidthBb,
            VelocityVb = section.VelocityVb,
            Kgm = section.Kgm,
            LeftBankHeightM = section.LeftBankHeightM,
            LeftFloodplainWidthM = section.LeftFloodplainWidthM,
            RightBankHeightM = section.RightBankHeightM,
            RightFloodplainWidthM = section.RightFloodplainWidthM,
            BankPoints = section.BankPoints.Select(point => new BankPointRecord
            {
                Id = point.Id,
                CrossSectionId = point.CrossSectionId,
                Side = point.Side,
                PointType = point.PointType,
                PointNumber = point.PointNumber,
                DistanceM = point.DistanceM,
                ElevationM = point.ElevationM
            }).ToList()
        }).ToArray();

    private static CrossSectionRecord Configure(
        CrossSectionRecord section,
        int number,
        double distance)
    {
        section.Number = number;
        section.DistanceFromHydroUnitM = distance.ToString(
            CultureInfo.InvariantCulture);
        return section;
    }

    private static CrossSectionHydraulicResult ResultFor(
        MultiCrossSectionHydraulicResult summary,
        int number) =>
        summary.Sections.Single(item => item.Number == number).HydraulicResult;

    private static string[] Snapshot(CrossSectionRecord section) =>
        section.BankPoints
            .Select(point =>
                $"{point.Side}|{point.PointType}|{point.PointNumber}|{point.DistanceM}|{point.ElevationM}")
            .Prepend(
                $"{section.Number}|{section.DistanceFromHydroUnitM}|{section.BottomLevelZb}|{section.DepthHb}|{section.WidthBb}|{section.VelocityVb}|{section.Kgm}")
            .ToArray();

    private static string[] Snapshot(CrossSectionGeometry geometry) =>
        geometry.Points
            .Select(point => $"{point.DistanceM:R}|{point.ElevationM:R}")
            .Concat(geometry.Segments.Select(segment =>
                $"{segment.Start.DistanceM:R},{segment.Start.ElevationM:R}" +
                $"->{segment.End.DistanceM:R},{segment.End.ElevationM:R}"))
            .ToArray();

    private static string Snapshot(HydraulicCalculationInput input) =>
        string.Join(
            "|",
            input.WaterLevel,
            input.Gravity,
            input.KinematicViscosity,
            input.DepthH,
            input.WidthB,
            input.HydraulicSlopeIf);

    private static void AssertResultsEqual(
        CrossSectionHydraulicResult expected,
        CrossSectionHydraulicResult actual)
    {
        Assert.Equal(expected.WaterLevel, actual.WaterLevel, 12);
        Assert.Equal(expected.Omega, actual.Omega, 12);
        Assert.Equal(expected.Chi, actual.Chi, 12);
        Assert.Equal(expected.HydraulicRadius, actual.HydraulicRadius, 12);
        Assert.Equal(expected.HydraulicSlope, actual.HydraulicSlope, 12);
        Assert.Equal(expected.ShearVelocity, actual.ShearVelocity, 12);
        Assert.Equal(expected.ChezyCoefficient, actual.ChezyCoefficient, 12);
        Assert.Equal(expected.Velocity, actual.Velocity, 12);
        Assert.Equal(expected.Discharge, actual.Discharge, 12);
    }

    private static void AssertResultsNotEqual(
        CrossSectionHydraulicResult expected,
        CrossSectionHydraulicResult actual)
    {
        Assert.True(
            new[]
            {
                expected.WaterLevel != actual.WaterLevel,
                expected.Omega != actual.Omega,
                expected.Chi != actual.Chi,
                expected.HydraulicRadius != actual.HydraulicRadius,
                expected.HydraulicSlope != actual.HydraulicSlope,
                expected.ShearVelocity != actual.ShearVelocity,
                expected.ChezyCoefficient != actual.ChezyCoefficient,
                expected.Velocity != actual.Velocity,
                expected.Discharge != actual.Discharge
            }.Any());
    }
}
