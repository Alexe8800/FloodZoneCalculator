using FloodZoneCalculator.Domain;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class ChezyDischargeIntegrationTests
{
    [Fact]
    public void Passes_chezy_velocity_to_discharge_calculator_for_profile_01()
    {
        var source = TestCrossSectionFactory.Symmetric();
        var geometry = new CrossSectionGeometryBuilder().Build(source);
        var result = new CrossSectionGeometryCalculator().Calculate(geometry, 106);
        var omega = Math.Round(result.Area, 6);
        var chi = Math.Round(result.GeometricPerimeterBelowWaterLevel, 6);
        var hydraulicRadius = new HydraulicRadiusCalculator().Calculate(omega, chi);

        var chezyCoefficient = GrishaninChezyCalculator.Calculate(
            9.81,
            1.15e-6,
            5.0,
            50.0);

        const double hydraulicSlope = 0.001;
        new HydraulicSlopeValidator().Validate(hydraulicSlope);
        var velocity = ChezyVelocityCalculator.Calculate(
            chezyCoefficient,
            hydraulicRadius,
            hydraulicSlope);
        var discharge = new DischargeCalculator().Calculate(velocity, omega);

        var expectedVelocity = 0.005378522138334274;
        var expectedDischarge = expectedVelocity * 426.666667;

        Assert.Equal(426.666667, omega, 6);
        Assert.Equal(94.913058, chi, 6);
        Assert.Equal(4.495342116150, hydraulicRadius, 6);
        Assert.Equal(expectedVelocity, velocity, 12);
        Assert.Equal(2.2948361141487976, expectedDischarge, 12);
        Assert.Equal(expectedDischarge, discharge, 12);
        Assert.True(discharge >= 0);
        Assert.False(double.IsNaN(discharge));
        Assert.False(double.IsInfinity(discharge));
    }

    [Fact]
    public void Passes_chezy_velocity_to_discharge_calculator_for_profile_02()
    {
        var source = TestCrossSectionFactory.Asymmetric();
        var geometry = new CrossSectionGeometryBuilder().Build(source);
        var result = new CrossSectionGeometryCalculator().Calculate(geometry, 105);
        var omega = result.Area;
        var chi = result.GeometricPerimeterBelowWaterLevel;
        var hydraulicRadius = new HydraulicRadiusCalculator().Calculate(omega, chi);

        var chezyCoefficient = GrishaninChezyCalculator.Calculate(
            9.81,
            1.15e-6,
            5.0,
            50.0);

        const double hydraulicSlope = 0.001;
        new HydraulicSlopeValidator().Validate(hydraulicSlope);
        var velocity = ChezyVelocityCalculator.Calculate(
            chezyCoefficient,
            hydraulicRadius,
            hydraulicSlope);
        var discharge = new DischargeCalculator().Calculate(velocity, omega);

        Assert.True(omega > 0);
        Assert.True(chi > 0);
        Assert.True(hydraulicRadius > 0);
        Assert.True(chezyCoefficient > 0);
        Assert.True(velocity >= 0);
        Assert.True(discharge >= 0);
        AssertFinite(omega, chi, hydraulicRadius, chezyCoefficient, velocity, discharge);
    }

    private static void AssertFinite(params double[] values)
    {
        Assert.All(values, value =>
        {
            Assert.False(double.IsNaN(value));
            Assert.False(double.IsInfinity(value));
        });
    }
}
