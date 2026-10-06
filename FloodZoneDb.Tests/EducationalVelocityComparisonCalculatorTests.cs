using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class EducationalVelocityComparisonCalculatorTests
{
    private readonly EducationalVelocityComparisonCalculator _calculator = new();

    [Fact]
    public void Calculates_observed_mean_difference_absolute_relative_difference_and_ratio()
    {
        var points = new[]
        {
            CreatePoint(1, 1, IsodatSide.Left, 0m, null, 1m),
            CreatePoint(2, 1, IsodatSide.Left, 4m, null, 3m),
            CreatePoint(3, 1, IsodatSide.Left, 4m, null, 5m)
        };

        var result = _calculator.Calculate(1, IsodatSide.Left, 2.0, points);

        Assert.Equal(3, result.ObservedCount);
        Assert.Equal(1.0, result.ObservedVelocityMin);
        Assert.Equal(5.0, result.ObservedVelocityMax);
        Assert.Equal(3.0, result.ObservedVelocityMean);
        Assert.Equal(1.0, result.Difference);
        Assert.Equal(1.0, result.AbsoluteDifference);
        Assert.Equal(0.5, result.RelativeDifference);
        Assert.Equal(1.5, result.Ratio);
    }

    [Fact]
    public void Keeps_zero_and_negative_observations_as_numeric_source_values()
    {
        var points = new[]
        {
            CreatePoint(1, 1, IsodatSide.Right, 0m, null, -2m),
            CreatePoint(2, 1, IsodatSide.Right, 1m, null, 0m)
        };

        var result = _calculator.Calculate(1, IsodatSide.Right, 2.0, points);

        Assert.Equal(-2.0, result.ObservedVelocityMin);
        Assert.Equal(0.0, result.ObservedVelocityMax);
        Assert.Equal(-1.0, result.ObservedVelocityMean);
        Assert.Equal(-3.0, result.Difference);
        Assert.Equal(3.0, result.AbsoluteDifference);
        Assert.Equal(1.5, result.RelativeDifference);
        Assert.Equal(-0.5, result.Ratio);
    }

    [Fact]
    public void Leaves_ratio_and_relative_difference_undefined_when_calculated_velocity_is_zero()
    {
        var result = _calculator.Calculate(
            1,
            IsodatSide.Left,
            0.0,
            new[] { CreatePoint(1, 1, IsodatSide.Left, 0m, null, 2m) });

        Assert.Equal(2.0, result.Difference);
        Assert.Equal(2.0, result.AbsoluteDifference);
        Assert.Null(result.RelativeDifference);
        Assert.Null(result.Ratio);
    }

    [Fact]
    public void Rejects_negative_calculated_velocity_as_unreachable_in_the_existing_hydraulic_model()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(
            1,
            IsodatSide.Left,
            -2.0,
            new[] { CreatePoint(1, 1, IsodatSide.Left, 0m, null, 1m) }));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Rejects_nonfinite_calculated_velocity(double velocity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(
            1,
            IsodatSide.Left,
            velocity,
            new[] { CreatePoint(1, 1, IsodatSide.Left, 0m, null, 1m) }));
    }

    [Fact]
    public void Rejects_missing_observed_velocity_and_invalid_arguments()
    {
        var depthOnly = new[] { CreatePoint(1, 1, IsodatSide.Left, 0m, 1m, null) };

        Assert.Throws<InvalidOperationException>(
            () => _calculator.Calculate(1, IsodatSide.Left, 1.0, depthOnly));
        Assert.Throws<ArgumentNullException>(
            () => _calculator.Calculate((EducationalFullHydraulicResult)null!, depthOnly));
        Assert.Throws<ArgumentNullException>(
            () => _calculator.Calculate(1, IsodatSide.Left, 1.0, null!));
        Assert.Throws<ArgumentException>(() => _calculator.Calculate(
            1,
            IsodatSide.Left,
            1.0,
            new IsodatHydraulicPoint[] { null! }));
    }

    [Fact]
    public void Calculates_profiles_independently_and_repeatedly_without_mutating_inputs()
    {
        var points = new[]
        {
            CreatePoint(1, 1, IsodatSide.Left, 0m, null, 0.4m),
            CreatePoint(2, 1, IsodatSide.Left, 0m, null, 0.6m),
            CreatePoint(3, 2, IsodatSide.Right, 2m, null, 1.0m)
        };
        var before = points.Select(Signature).ToArray();

        var first = _calculator.Calculate(1, IsodatSide.Left, 0.5, points);
        _calculator.Calculate(2, IsodatSide.Right, 1.5, points);
        var repeated = _calculator.Calculate(1, IsodatSide.Left, 0.5, points);

        Assert.Equal(0.5, first.ObservedVelocityMean);
        Assert.Equal(first.ObservedVelocityMean, repeated.ObservedVelocityMean);
        Assert.Equal(first.Difference, repeated.Difference);
        Assert.Equal(first.Ratio, repeated.Ratio);
        Assert.Equal(before, points.Select(Signature));
    }

    private static IsodatHydraulicPoint CreatePoint(
        long id,
        int section,
        IsodatSide side,
        decimal x,
        decimal? depth,
        decimal? velocity) =>
        new(
            id,
            section,
            side,
            x,
            depth,
            velocity,
            id,
            new string('a', 64),
            "Sheet1",
            checked((int)id),
            depth.HasValue ? "Depth" : "Velocity");

    private static string Signature(IsodatHydraulicPoint point) =>
        $"{point.SourceIsodatId}|{point.X}|{point.DepthH}|{point.ObservedVelocity}";
}
