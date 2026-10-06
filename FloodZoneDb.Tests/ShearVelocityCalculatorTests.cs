using FloodZoneCalculator.Domain;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class ShearVelocityCalculatorTests
{
    [Fact]
    public void Calculates_shear_velocity_for_positive_inputs()
    {
        var result = new ShearVelocityCalculator().Calculate(4, 0.01, 9.81);
        var expected = Math.Sqrt(9.81 * 4 * 0.01);

        Assert.Equal(expected, result, 6);
    }

    [Fact]
    public void Returns_zero_when_hydraulic_radius_is_zero()
    {
        var result = new ShearVelocityCalculator().Calculate(0, 0.01, 9.81);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Returns_zero_when_hydraulic_slope_is_zero()
    {
        var result = new ShearVelocityCalculator().Calculate(4, 0, 9.81);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Rejects_negative_hydraulic_radius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShearVelocityCalculator().Calculate(-1, 0.01, 9.81));
    }

    [Fact]
    public void Rejects_negative_radicand_from_negative_hydraulic_slope()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShearVelocityCalculator().Calculate(4, -0.01, 9.81));
    }

    [Fact]
    public void Allows_negative_hydraulic_slope_when_zero_radius_keeps_radicand_zero()
    {
        var result = new ShearVelocityCalculator().Calculate(0, -0.01, 9.81);

        Assert.Equal(0, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-9.81)]
    public void Rejects_non_positive_gravity(double gravity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShearVelocityCalculator().Calculate(4, 0.01, gravity));
    }

    [Theory]
    [InlineData(double.NaN, 0.01, 9.81)]
    [InlineData(4, double.NaN, 9.81)]
    [InlineData(4, 0.01, double.NaN)]
    [InlineData(double.PositiveInfinity, 0.01, 9.81)]
    [InlineData(4, double.PositiveInfinity, 9.81)]
    [InlineData(4, 0.01, double.PositiveInfinity)]
    public void Rejects_non_finite_inputs(double hydraulicRadius, double hydraulicSlope, double gravity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShearVelocityCalculator().Calculate(hydraulicRadius, hydraulicSlope, gravity));
    }

    [Fact]
    public void Rejects_inputs_that_overflow_the_radicand()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ShearVelocityCalculator().Calculate(
                double.MaxValue,
                double.MaxValue,
                double.MaxValue));
    }

    [Fact]
    public void Positive_inputs_produce_a_finite_result()
    {
        var result = new ShearVelocityCalculator().Calculate(4.495342116, 0.001, 9.81);

        Assert.False(double.IsNaN(result));
        Assert.False(double.IsInfinity(result));
    }
}
