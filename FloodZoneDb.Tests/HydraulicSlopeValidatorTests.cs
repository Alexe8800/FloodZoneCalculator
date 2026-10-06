using FloodZoneCalculator.Domain;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class HydraulicSlopeValidatorTests
{
    private readonly HydraulicSlopeValidator validator = new();

    [Fact]
    public void Accepts_positive_finite_hydraulic_slope()
    {
        var exception = Record.Exception(() => validator.Validate(0.001));

        Assert.Null(exception);
    }

    [Fact]
    public void Accepts_zero_hydraulic_slope()
    {
        var exception = Record.Exception(() => validator.Validate(0));

        Assert.Null(exception);
    }

    [Fact]
    public void Accepts_negative_finite_hydraulic_slope_until_methodology_defines_a_sign_rule()
    {
        var exception = Record.Exception(() => validator.Validate(-0.001));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0.0001)]
    [InlineData(0.001)]
    [InlineData(0.01)]
    public void Accepts_ordinary_finite_values(double hydraulicSlope)
    {
        var exception = Record.Exception(() => validator.Validate(hydraulicSlope));

        Assert.Null(exception);
        Assert.False(double.IsNaN(hydraulicSlope));
        Assert.False(double.IsInfinity(hydraulicSlope));
    }

    [Fact]
    public void Rejects_nan()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => validator.Validate(double.NaN));
    }

    [Fact]
    public void Rejects_positive_infinity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            validator.Validate(double.PositiveInfinity));
    }

    [Fact]
    public void Rejects_negative_infinity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            validator.Validate(double.NegativeInfinity));
    }
}
