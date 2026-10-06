using System;
using System.Linq;
using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class CrossSectionHydraulicContextTests
{
    private static HydraulicCalculationInput Input() =>
        new(106, 9.81, 1.15e-6, 5, 50, 0.001);

    [Fact]
    public void Creates_context_with_geometry_and_input_references()
    {
        var geometry = Build(TestCrossSectionFactory.Symmetric());
        var input = Input();

        var context = new CrossSectionHydraulicContext(geometry, input);

        Assert.Same(geometry, context.Geometry);
        Assert.Same(input, context.Input);
    }

    [Fact]
    public void Rejects_null_geometry()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new CrossSectionHydraulicContext(null!, Input()));

        Assert.Equal("geometry", exception.ParamName);
    }

    [Fact]
    public void Rejects_null_input()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new CrossSectionHydraulicContext(
                Build(TestCrossSectionFactory.Symmetric()),
                null!));

        Assert.Equal("input", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(AllProfiles))]
    public void Context_calculation_matches_geometry_and_input_calculation(
        Func<CrossSectionRecord> profileFactory)
    {
        var geometry = Build(profileFactory());
        var input = Input();
        var context = new CrossSectionHydraulicContext(geometry, input);
        var calculator = new CrossSectionHydraulicResultCalculator();

        var byContext = calculator.Calculate(context);
        var direct = calculator.Calculate(geometry, input);

        AssertResultsEqual(direct, byContext);
    }

    [Fact]
    public void Incomplete_profile_is_not_completed_by_context()
    {
        var geometry = Build(TestCrossSectionFactory.Incomplete());
        var context = new CrossSectionHydraulicContext(geometry, Input());

        _ = new CrossSectionHydraulicResultCalculator().Calculate(context);

        Assert.Equal(5, geometry.Points.Count);
        Assert.Equal(4, geometry.Segments.Count);
    }

    [Fact]
    public void Reusing_context_is_repeatable_and_has_no_side_effects()
    {
        var geometry = Build(TestCrossSectionFactory.Symmetric());
        var input = Input();
        var context = new CrossSectionHydraulicContext(geometry, input);
        var geometrySnapshot = Snapshot(geometry);
        var calculator = new CrossSectionHydraulicResultCalculator();

        var first = calculator.Calculate(context);
        var second = calculator.Calculate(context);

        AssertResultsEqual(first, second);
        Assert.Equal(geometrySnapshot, Snapshot(geometry));
        Assert.Equal(106, input.WaterLevel);
        Assert.Equal(9.81, input.Gravity);
        Assert.Equal(1.15e-6, input.KinematicViscosity);
        Assert.Equal(5, input.DepthH);
        Assert.Equal(50, input.WidthB);
        Assert.Equal(0.001, input.HydraulicSlopeIf);
    }

    [Fact]
    public void Distance_metadata_does_not_change_context_result()
    {
        var first = TestCrossSectionFactory.Symmetric();
        var second = TestCrossSectionFactory.Symmetric();
        first.DistanceFromHydroUnitM = "0";
        second.DistanceFromHydroUnitM = "7000";
        var calculator = new CrossSectionHydraulicResultCalculator();
        var input = Input();

        var firstResult = calculator.Calculate(new CrossSectionHydraulicContext(
            Build(first), input));
        var secondResult = calculator.Calculate(new CrossSectionHydraulicContext(
            Build(second), input));

        AssertResultsEqual(firstResult, secondResult);
    }

    public static TheoryData<Func<CrossSectionRecord>> AllProfiles =>
        new()
        {
            () => TestCrossSectionFactory.Symmetric(),
            () => TestCrossSectionFactory.Asymmetric(),
            () => TestCrossSectionFactory.Incomplete(),
            () => TestCrossSectionFactory.FlatBottom(),
            () => TestCrossSectionFactory.SlopedBottom()
        };

    private static CrossSectionGeometry Build(CrossSectionRecord section) =>
        new CrossSectionGeometryBuilder().Build(section);

    private static string Snapshot(CrossSectionGeometry geometry) =>
        string.Join(
            "|",
            geometry.Points.Select(point => $"{point.DistanceM:R},{point.ElevationM:R}")) +
        "#" +
        string.Join(
            "|",
            geometry.Segments.Select(segment =>
                $"{segment.Start.DistanceM:R},{segment.Start.ElevationM:R}" +
                $"->{segment.End.DistanceM:R},{segment.End.ElevationM:R}"));

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
}
