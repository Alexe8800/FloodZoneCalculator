using FloodZoneCalculator.Domain;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class CrossSectionHydraulicResultCalculatorTests
{
    [Fact]
    public void Calculates_profile_01_result_with_independent_control_values()
    {
        var source = TestCrossSectionFactory.Symmetric();
        var geometry = new CrossSectionGeometryBuilder().Build(source);

        var result = new CrossSectionHydraulicResultCalculator().Calculate(
            geometry,
            waterLevel: 106,
            gravity: 9.81,
            kinematicViscosity: 1.15e-6,
            depth: 5.0,
            width: 50.0,
            hydraulicSlope: 0.001);

        Assert.Equal(106, result.WaterLevel);
        Assert.Equal(426.666667, result.Omega, 6);
        Assert.Equal(94.913058, result.Chi, 6);
        Assert.Equal(4.495342116150, result.HydraulicRadius, 6);
        Assert.Equal(0.001, result.HydraulicSlope);
        Assert.Equal(
            Math.Sqrt(9.81 * (426.666667 / 94.913058) * 0.001),
            result.ShearVelocity,
            6);
        Assert.Equal(0.08021980204997178, result.ChezyCoefficient, 12);
        Assert.Equal(
            0.08021980204997178
                * Math.Sqrt((426.666667 / 94.913058) * 0.001),
            result.Velocity,
            6);
        Assert.Equal(
            0.08021980204997178
                * Math.Sqrt((426.666667 / 94.913058) * 0.001)
                * 426.666667,
            result.Discharge,
            6);
        AssertFinite(
            result.Omega,
            result.Chi,
            result.HydraulicRadius,
            result.HydraulicSlope,
            result.ShearVelocity,
            result.ChezyCoefficient,
            result.Velocity,
            result.Discharge);
    }

    [Fact]
    public void Calculates_profile_02_result_without_assuming_symmetry()
    {
        var geometry = new CrossSectionGeometryBuilder().Build(
            TestCrossSectionFactory.Asymmetric());

        var result = Calculate(geometry, 105);

        Assert.True(result.Omega > 0);
        Assert.True(result.Chi > 0);
        Assert.True(result.HydraulicRadius > 0);
        Assert.True(result.ChezyCoefficient > 0);
        Assert.True(result.Velocity >= 0);
        Assert.True(result.Discharge >= 0);
        AssertFinite(
            result.Omega,
            result.Chi,
            result.HydraulicRadius,
            result.HydraulicSlope,
            result.ShearVelocity,
            result.ChezyCoefficient,
            result.Velocity,
            result.Discharge);
    }

    [Fact]
    public void Calculates_profile_03_using_only_existing_points_and_segments()
    {
        var geometry = new CrossSectionGeometryBuilder().Build(
            TestCrossSectionFactory.Incomplete());

        var result = Calculate(geometry, 106);

        Assert.Equal(5, geometry.Points.Count);
        Assert.Equal(4, geometry.Segments.Count);
        Assert.True(result.Omega > 0);
        Assert.True(result.Chi > 0);
        AssertFinite(
            result.Omega,
            result.Chi,
            result.HydraulicRadius,
            result.ShearVelocity,
            result.ChezyCoefficient,
            result.Velocity,
            result.Discharge);
    }

    private static CrossSectionHydraulicResult Calculate(
        CrossSectionGeometry geometry,
        double waterLevel) =>
        new CrossSectionHydraulicResultCalculator().Calculate(
            geometry,
            waterLevel,
            gravity: 9.81,
            kinematicViscosity: 1.15e-6,
            depth: 5.0,
            width: 50.0,
            hydraulicSlope: 0.001);

    private static void AssertFinite(params double[] values)
    {
        Assert.All(values, value =>
        {
            Assert.False(double.IsNaN(value));
            Assert.False(double.IsInfinity(value));
        });
    }
}
