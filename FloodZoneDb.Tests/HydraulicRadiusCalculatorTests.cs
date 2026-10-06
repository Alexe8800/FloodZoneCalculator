using FloodZoneCalculator.Domain;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class HydraulicRadiusCalculatorTests
{
    [Fact]
    public void Calculates_radius_for_symmetric_profile_geometry_values()
    {
        var result = new HydraulicRadiusCalculator().Calculate(426.666667, 94.913058);

        Assert.Equal(426.666667 / 94.913058, result, 6);
        Assert.False(double.IsNaN(result));
        Assert.False(double.IsInfinity(result));
    }

    [Fact]
    public void Returns_zero_for_zero_area_and_positive_perimeter()
    {
        var result = new HydraulicRadiusCalculator().Calculate(0, 10);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Rejects_zero_perimeter()
    {
        Assert.Throws<DivideByZeroException>(() =>
            new HydraulicRadiusCalculator().Calculate(100, 0));
    }

    [Fact]
    public void Rejects_negative_area()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HydraulicRadiusCalculator().Calculate(-1, 10));
    }

    [Fact]
    public void Rejects_negative_perimeter()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HydraulicRadiusCalculator().Calculate(100, -1));
    }

    [Fact]
    public void Calculates_simple_positive_case()
    {
        var result = new HydraulicRadiusCalculator().Calculate(100, 20);

        Assert.Equal(5, result);
    }

    [Theory]
    [InlineData(double.NaN, 10)]
    [InlineData(double.PositiveInfinity, 10)]
    [InlineData(100, double.NaN)]
    [InlineData(100, double.PositiveInfinity)]
    public void Rejects_non_finite_inputs(double omega, double chi)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new HydraulicRadiusCalculator().Calculate(omega, chi));
    }
}
