using FloodZoneCalculator.Domain;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class ChezyVelocityCalculatorTests
{
    [Fact]
    public void Calculates_the_control_case()
    {
        var result = ChezyVelocityCalculator.Calculate(
            0.08021980204997178,
            4.495342116150,
            0.001);

        Assert.Equal(0.005378522138334274, result, 12);
        Assert.True(result > 0);
        Assert.False(double.IsNaN(result));
        Assert.False(double.IsInfinity(result));
    }

    [Fact]
    public void Returns_zero_when_radius_is_zero()
    {
        Assert.Equal(0, ChezyVelocityCalculator.Calculate(1, 0, 0.001));
    }

    [Fact]
    public void Returns_zero_when_slope_is_zero()
    {
        Assert.Equal(0, ChezyVelocityCalculator.Calculate(1, 4, 0));
    }

    [Fact]
    public void Returns_zero_when_radius_and_slope_are_zero()
    {
        Assert.Equal(0, ChezyVelocityCalculator.Calculate(1, 0, 0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_chezy_coefficient(double C)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ChezyVelocityCalculator.Calculate(C, 4, 0.001));
    }

    [Fact]
    public void Rejects_negative_radius()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ChezyVelocityCalculator.Calculate(1, -1, 0.001));
    }

    [Fact]
    public void Rejects_negative_radicand_from_negative_slope()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ChezyVelocityCalculator.Calculate(1, 4, -0.001));
    }

    [Theory]
    [InlineData(double.NaN, 4, 0.001)]
    [InlineData(1, double.NaN, 0.001)]
    [InlineData(1, 4, double.NaN)]
    [InlineData(double.PositiveInfinity, 4, 0.001)]
    [InlineData(1, double.PositiveInfinity, 0.001)]
    [InlineData(1, 4, double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity, 4, 0.001)]
    [InlineData(1, double.NegativeInfinity, 0.001)]
    [InlineData(1, 4, double.NegativeInfinity)]
    public void Rejects_non_finite_inputs(double C, double R, double If)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ChezyVelocityCalculator.Calculate(C, R, If));
    }

    [Fact]
    public void Rejects_non_finite_result_from_overflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ChezyVelocityCalculator.Calculate(double.MaxValue, double.MaxValue, 1));
    }

    [Fact]
    public void Increases_when_chezy_coefficient_increases()
    {
        var lower = ChezyVelocityCalculator.Calculate(1, 4, 0.001);
        var higher = ChezyVelocityCalculator.Calculate(2, 4, 0.001);

        Assert.True(higher > lower);
    }

    [Fact]
    public void Increases_when_radius_increases()
    {
        var lower = ChezyVelocityCalculator.Calculate(1, 4, 0.001);
        var higher = ChezyVelocityCalculator.Calculate(1, 9, 0.001);

        Assert.True(higher > lower);
    }

    [Fact]
    public void Increases_when_positive_slope_increases()
    {
        var lower = ChezyVelocityCalculator.Calculate(1, 4, 0.001);
        var higher = ChezyVelocityCalculator.Calculate(1, 4, 0.004);

        Assert.True(higher > lower);
    }
}
