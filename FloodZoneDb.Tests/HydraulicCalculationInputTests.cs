using System;
using System.Linq;
using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class HydraulicCalculationInputTests
{
    private static HydraulicCalculationInput SyntheticInput(
        double waterLevel = 106,
        double gravity = 9.81,
        double kinematicViscosity = 1.15e-6,
        double depthH = 5,
        double widthB = 50,
        double hydraulicSlopeIf = 0.001) =>
        new(
            waterLevel,
            gravity,
            kinematicViscosity,
            depthH,
            widthB,
            hydraulicSlopeIf);

    [Fact]
    public void Creates_read_only_synthetic_input_with_explicit_values()
    {
        var input = SyntheticInput();

        Assert.Equal(106, input.WaterLevel);
        Assert.Equal(9.81, input.Gravity);
        Assert.Equal(1.15e-6, input.KinematicViscosity);
        Assert.Equal(5, input.DepthH);
        Assert.Equal(50, input.WidthB);
        Assert.Equal(0.001, input.HydraulicSlopeIf);
    }

    [Theory]
    [InlineData(double.NaN, "waterLevel")]
    [InlineData(double.PositiveInfinity, "gravity")]
    [InlineData(double.NegativeInfinity, "kinematicViscosity")]
    public void Rejects_non_finite_input_values(double value, string parameterName)
    {
        var values = new[] { 106d, 9.81, 1.15e-6, 5d, 50d, 0.001d };
        var index = parameterName switch
        {
            "waterLevel" => 0,
            "gravity" => 1,
            "kinematicViscosity" => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(parameterName))
        };
        values[index] = value;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HydraulicCalculationInput(
                values[0], values[1], values[2], values[3], values[4], values[5]));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Theory]
    [InlineData(0, "gravity")]
    [InlineData(-1, "kinematicViscosity")]
    [InlineData(0, "depthH")]
    [InlineData(-1, "widthB")]
    public void Rejects_non_positive_values_required_by_existing_calculators(
        double value,
        string parameterName)
    {
        var input = new[] { 106d, 9.81, 1.15e-6, 5d, 50d, 0.001d };
        var index = parameterName switch
        {
            "gravity" => 1,
            "kinematicViscosity" => 2,
            "depthH" => 3,
            "widthB" => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(parameterName))
        };
        input[index] = value;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HydraulicCalculationInput(
                input[0], input[1], input[2], input[3], input[4], input[5]));

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Fact]
    public void Single_section_orchestrator_matches_stage_216_result()
    {
        var section = TestCrossSectionFactory.Symmetric();
        var geometry = new CrossSectionGeometryBuilder().Build(section);
        var input = SyntheticInput();

        var result = new CrossSectionHydraulicResultCalculator()
            .Calculate(geometry, input);

        Assert.Equal(426.666667, result.Omega, 6);
        Assert.Equal(94.913058, result.Chi, 6);
        Assert.Equal(4.495342128, result.HydraulicRadius, 6);
        Assert.Equal(0.209998348, result.ShearVelocity, 6);
        Assert.Equal(0.08021980205, result.ChezyCoefficient, 10);
        Assert.Equal(0.005378522145, result.Velocity, 9);
        Assert.Equal(2.294836115, result.Discharge, 6);
    }

    [Fact]
    public void Multi_section_orchestrator_uses_one_input_for_all_five_sections()
    {
        var sections = new[]
        {
            Configure(TestCrossSectionFactory.FlatBottom(), 4, 4000),
            Configure(TestCrossSectionFactory.Symmetric(), 1, 0),
            Configure(TestCrossSectionFactory.SlopedBottom(), 5, 7000),
            Configure(TestCrossSectionFactory.Incomplete(), 3, 2500),
            Configure(TestCrossSectionFactory.Asymmetric(), 2, 1000)
        };
        var input = SyntheticInput();

        var result = new MultiCrossSectionHydraulicResultCalculator()
            .Calculate(sections, input);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result.Sections.Select(item => item.Number));
        Assert.All(result.Sections, item => Assert.Equal(106, item.HydraulicResult.WaterLevel));
        Assert.Equal(426.666667, result.Sections[0].HydraulicResult.Omega, 6);
        Assert.Equal(438, result.Sections[1].HydraulicResult.Omega, 6);
        Assert.Equal(400, result.Sections[2].HydraulicResult.Omega, 6);
        Assert.Equal(465, result.Sections[3].HydraulicResult.Omega, 6);
        Assert.Equal(500.5, result.Sections[4].HydraulicResult.Omega, 6);
    }

    [Fact]
    public void Changing_water_level_recalculates_geometry_dependent_values_only()
    {
        var section = TestCrossSectionFactory.Symmetric();
        var geometry = new CrossSectionGeometryBuilder().Build(section);
        var at106 = new CrossSectionHydraulicResultCalculator().Calculate(
            geometry, SyntheticInput(waterLevel: 106));
        var at105 = new CrossSectionHydraulicResultCalculator().Calculate(
            geometry, SyntheticInput(waterLevel: 105));

        Assert.NotEqual(at106.Omega, at105.Omega);
        Assert.NotEqual(at106.Chi, at105.Chi);
        Assert.NotEqual(at106.HydraulicRadius, at105.HydraulicRadius);
        Assert.Equal(at106.ChezyCoefficient, at105.ChezyCoefficient, 12);
        Assert.Equal(106, at106.WaterLevel);
        Assert.Equal(105, at105.WaterLevel);
    }

    [Fact]
    public void Changing_slope_does_not_change_geometry_radius_or_chezy()
    {
        var geometry = new CrossSectionGeometryBuilder()
            .Build(TestCrossSectionFactory.Symmetric());
        var baseline = new CrossSectionHydraulicResultCalculator().Calculate(
            geometry, SyntheticInput(hydraulicSlopeIf: 0.001));
        var changed = new CrossSectionHydraulicResultCalculator().Calculate(
            geometry, SyntheticInput(hydraulicSlopeIf: 0.002));

        Assert.Equal(baseline.Omega, changed.Omega, 12);
        Assert.Equal(baseline.Chi, changed.Chi, 12);
        Assert.Equal(baseline.HydraulicRadius, changed.HydraulicRadius, 12);
        Assert.Equal(baseline.ChezyCoefficient, changed.ChezyCoefficient, 12);
        Assert.NotEqual(baseline.ShearVelocity, changed.ShearVelocity);
        Assert.NotEqual(baseline.Velocity, changed.Velocity);
        Assert.NotEqual(baseline.Discharge, changed.Discharge);
    }

    [Fact]
    public void Changing_depth_or_width_changes_chezy_dependent_values_only()
    {
        var geometry = new CrossSectionGeometryBuilder()
            .Build(TestCrossSectionFactory.Symmetric());
        var baseline = new CrossSectionHydraulicResultCalculator().Calculate(
            geometry, SyntheticInput());
        var changedDepth = new CrossSectionHydraulicResultCalculator().Calculate(
            geometry, SyntheticInput(depthH: 6));
        var changedWidth = new CrossSectionHydraulicResultCalculator().Calculate(
            geometry, SyntheticInput(widthB: 60));

        Assert.Equal(baseline.HydraulicRadius, changedDepth.HydraulicRadius, 12);
        Assert.Equal(baseline.ShearVelocity, changedDepth.ShearVelocity, 12);
        Assert.NotEqual(baseline.ChezyCoefficient, changedDepth.ChezyCoefficient);
        Assert.NotEqual(baseline.Velocity, changedDepth.Velocity);
        Assert.NotEqual(baseline.Discharge, changedDepth.Discharge);

        Assert.Equal(baseline.HydraulicRadius, changedWidth.HydraulicRadius, 12);
        Assert.Equal(baseline.ShearVelocity, changedWidth.ShearVelocity, 12);
        Assert.NotEqual(baseline.ChezyCoefficient, changedWidth.ChezyCoefficient);
        Assert.NotEqual(baseline.Velocity, changedWidth.Velocity);
        Assert.NotEqual(baseline.Discharge, changedWidth.Discharge);
    }

    [Fact]
    public void Distance_is_not_a_hydraulic_input_and_does_not_change_result()
    {
        var first = Configure(TestCrossSectionFactory.Symmetric(), 1, 0);
        var second = Configure(TestCrossSectionFactory.Symmetric(), 1, 7000);
        var calculator = new MultiCrossSectionHydraulicResultCalculator();
        var input = SyntheticInput();

        var firstResult = calculator.Calculate(new[] { first }, input).Sections[0].HydraulicResult;
        var secondResult = calculator.Calculate(new[] { second }, input).Sections[0].HydraulicResult;

        Assert.Equal(firstResult.Omega, secondResult.Omega, 12);
        Assert.Equal(firstResult.Chi, secondResult.Chi, 12);
        Assert.Equal(firstResult.HydraulicRadius, secondResult.HydraulicRadius, 12);
        Assert.Equal(firstResult.ShearVelocity, secondResult.ShearVelocity, 12);
        Assert.Equal(firstResult.ChezyCoefficient, secondResult.ChezyCoefficient, 12);
        Assert.Equal(firstResult.Velocity, secondResult.Velocity, 12);
        Assert.Equal(firstResult.Discharge, secondResult.Discharge, 12);
    }

    [Fact]
    public void Input_and_sections_are_not_mutated_and_repeat_calculation_is_stable()
    {
        var sections = new[]
        {
            Configure(TestCrossSectionFactory.Asymmetric(), 2, 1000),
            Configure(TestCrossSectionFactory.Symmetric(), 1, 0)
        };
        var originalOrder = sections.Select(section => section.Number).ToArray();
        var originalDistances = sections.Select(section => section.DistanceFromHydroUnitM).ToArray();
        var input = SyntheticInput();
        var calculator = new MultiCrossSectionHydraulicResultCalculator();

        var first = calculator.Calculate(sections, input);
        var second = calculator.Calculate(sections, input);

        Assert.Equal(originalOrder, sections.Select(section => section.Number));
        Assert.Equal(originalDistances, sections.Select(section => section.DistanceFromHydroUnitM));
        Assert.Equal(106, input.WaterLevel);
        Assert.Equal(9.81, input.Gravity);
        Assert.Equal(first.Sections[0].HydraulicResult.Discharge,
            second.Sections[0].HydraulicResult.Discharge, 12);
    }

    private static CrossSectionRecord Configure(
        CrossSectionRecord section,
        int number,
        double distance)
    {
        section.Number = number;
        section.DistanceFromHydroUnitM = distance.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        return section;
    }
}
