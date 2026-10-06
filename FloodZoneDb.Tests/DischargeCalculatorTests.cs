using FloodZoneCalculator.Domain;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class DischargeCalculatorTests
{
    [Fact]
    public void Calculates_discharge_for_simple_positive_values()
    {
        var result = new DischargeCalculator().Calculate(2, 100);

        Assert.Equal(200, result);
    }

    [Fact]
    public void Returns_zero_when_velocity_is_zero()
    {
        var result = new DischargeCalculator().Calculate(0, 100);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Returns_zero_when_wetted_area_is_zero()
    {
        var result = new DischargeCalculator().Calculate(2, 0);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Rejects_negative_velocity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DischargeCalculator().Calculate(-1, 100));
    }

    [Fact]
    public void Rejects_negative_wetted_area()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DischargeCalculator().Calculate(2, -1));
    }

    [Theory]
    [InlineData(double.NaN, 100)]
    [InlineData(double.PositiveInfinity, 100)]
    [InlineData(2, double.NaN)]
    [InlineData(2, double.PositiveInfinity)]
    public void Rejects_non_finite_inputs(double velocity, double wettedArea)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DischargeCalculator().Calculate(velocity, wettedArea));
    }

    [Fact]
    public void Rejects_product_overflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DischargeCalculator().Calculate(double.MaxValue, double.MaxValue));
    }

    [Fact]
    public void Positive_inputs_produce_a_finite_result()
    {
        var result = new DischargeCalculator().Calculate(2, 426.666667);

        Assert.Equal(853.333334, result, 6);
        Assert.False(double.IsNaN(result));
        Assert.False(double.IsInfinity(result));
    }
}
