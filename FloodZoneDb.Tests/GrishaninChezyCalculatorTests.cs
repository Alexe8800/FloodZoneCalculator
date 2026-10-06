using FloodZoneCalculator.Domain;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class GrishaninChezyCalculatorTests
{
    [Fact]
    public void Calculates_the_methodology_control_case()
    {
        var result = GrishaninChezyCalculator.Calculate(9.81, 1.15e-6, 5.0, 50.0);

        Assert.Equal(0.08021980204997178, result, 12);
        Assert.True(result > 0);
        Assert.False(double.IsNaN(result));
        Assert.False(double.IsInfinity(result));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_gravity(double g)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GrishaninChezyCalculator.Calculate(g, 1.15e-6, 5, 50));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_kinematic_viscosity(double nu)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GrishaninChezyCalculator.Calculate(9.81, nu, 5, 50));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_depth(double h)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GrishaninChezyCalculator.Calculate(9.81, 1.15e-6, h, 50));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Rejects_non_positive_width(double B)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GrishaninChezyCalculator.Calculate(9.81, 1.15e-6, 5, B));
    }

    [Theory]
    [InlineData(double.NaN, 1.15e-6, 5, 50)]
    [InlineData(9.81, double.NaN, 5, 50)]
    [InlineData(9.81, 1.15e-6, double.NaN, 50)]
    [InlineData(9.81, 1.15e-6, 5, double.NaN)]
    [InlineData(double.PositiveInfinity, 1.15e-6, 5, 50)]
    [InlineData(9.81, double.PositiveInfinity, 5, 50)]
    [InlineData(9.81, 1.15e-6, double.PositiveInfinity, 50)]
    [InlineData(9.81, 1.15e-6, 5, double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity, 1.15e-6, 5, 50)]
    [InlineData(9.81, double.NegativeInfinity, 5, 50)]
    [InlineData(9.81, 1.15e-6, double.NegativeInfinity, 50)]
    [InlineData(9.81, 1.15e-6, 5, double.NegativeInfinity)]
    public void Rejects_non_finite_inputs(double g, double nu, double h, double B)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GrishaninChezyCalculator.Calculate(g, nu, h, B));
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(5, 50)]
    [InlineData(10, 100)]
    public void Produces_finite_positive_values_for_valid_inputs(double h, double B)
    {
        var result = GrishaninChezyCalculator.Calculate(9.81, 1.15e-6, h, B);

        Assert.True(result > 0);
        Assert.False(double.IsNaN(result));
        Assert.False(double.IsInfinity(result));
    }

    [Fact]
    public void Increases_when_depth_increases_with_other_inputs_fixed()
    {
        var lowerDepth = GrishaninChezyCalculator.Calculate(9.81, 1.15e-6, 5, 50);
        var higherDepth = GrishaninChezyCalculator.Calculate(9.81, 1.15e-6, 10, 50);

        Assert.True(higherDepth > lowerDepth);
    }

    [Fact]
    public void Decreases_when_width_increases_with_other_inputs_fixed()
    {
        var narrowerWidth = GrishaninChezyCalculator.Calculate(9.81, 1.15e-6, 5, 50);
        var widerWidth = GrishaninChezyCalculator.Calculate(9.81, 1.15e-6, 5, 100);

        Assert.True(widerWidth < narrowerWidth);
    }

    [Fact]
    public void Rejects_an_intermediate_overflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GrishaninChezyCalculator.Calculate(double.MaxValue, double.MaxValue, 1, 1));
    }
}
