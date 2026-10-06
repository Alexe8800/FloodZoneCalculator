using FloodZoneCalculator.Domain;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class ChezyVelocityIntegrationTests
{
    [Fact]
    public void Calculates_velocity_from_profile_geometry_and_grishanin_chezy_coefficient()
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

        var expected = 0.08021980204997178
            * Math.Sqrt((426.666667 / 94.913058) * 0.001);

        Assert.Equal(426.666667, omega, 6);
        Assert.Equal(94.913058, chi, 6);
        Assert.Equal(4.495342116150, hydraulicRadius, 6);
        Assert.Equal(expected, velocity, 12);
        Assert.True(velocity > 0);
        Assert.False(double.IsNaN(velocity));
        Assert.False(double.IsInfinity(velocity));
    }
}
