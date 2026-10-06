using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class EducationalDepthHydraulicCalculatorTests
{
    private readonly EducationalDepthHydraulicCalculator _calculator = new();

    [Fact]
    public void Creates_six_depth_profiles_with_eight_depth_points_per_profile()
    {
        var sourcePoints = CreateRealisticPointSet();

        var profiles = EducationalDepthProfileGrouper.Group(sourcePoints);

        Assert.Equal(6, profiles.Count);
        foreach (var section in new[] { 1, 2, 3 })
        foreach (var side in Enum.GetValues<IsodatSide>())
        {
            var profile = Assert.Single(profiles, item =>
                item.CrossSectionNumber == section && item.Side == side);
            Assert.Equal(8, profile.Points.Count);
            Assert.All(profile.Points, point => Assert.True(point.DepthH >= 0m));
        }
    }

    [Fact]
    public void Sorts_a_copy_by_x_and_preserves_repeated_points_and_input_order()
    {
        var sourcePoints = new[]
        {
            CreatePoint(1, 1, IsodatSide.Left, 8m, 2m),
            CreatePoint(2, 1, IsodatSide.Left, 3m, 4m),
            CreatePoint(3, 1, IsodatSide.Left, 3m, 5m)
        };
        var originalSignature = sourcePoints.Select(Signature).ToArray();

        var profile = new EducationalDepthProfile(1, IsodatSide.Left, sourcePoints);

        Assert.Equal(new[] { 8m, 3m, 3m }, profile.Points.Select(point => point.X));
        Assert.Equal(new[] { 3m, 3m, 8m }, profile.PointsSortedByX.Select(point => point.X));
        Assert.Equal(3, profile.Points.Count);
        Assert.Equal(originalSignature, sourcePoints.Select(Signature));
        Assert.Equal(IsodatHydraulicPoint.EducationalSyntheticInterpretation, profile.Interpretation);
    }

    [Fact]
    public void Clips_crossing_segment_for_area_and_wetted_profile_perimeter_without_persisting_intersection()
    {
        var profile = CreateProfile(
            CreatePoint(1, 1, IsodatSide.Left, 0m, 3m),
            CreatePoint(2, 1, IsodatSide.Left, 4m, 7m));
        var originalPoints = profile.Points.ToArray();

        var result = _calculator.Calculate(profile, 5.0);

        Assert.Equal(8.0, result.Omega, 10);
        Assert.Equal(Math.Sqrt(8.0), result.Chi, 10);
        Assert.Equal(2, result.PointCount);
        Assert.Equal(originalPoints, profile.Points);
    }

    [Fact]
    public void Changing_educational_water_depth_changes_area_perimeter_and_radius()
    {
        var profile = CreateProfile(
            CreatePoint(1, 1, IsodatSide.Left, 0m, 3m),
            CreatePoint(2, 1, IsodatSide.Left, 4m, 7m));

        var atFive = _calculator.Calculate(profile, 5.0);
        var atSix = _calculator.Calculate(profile, 6.0);

        Assert.NotEqual(atFive.Omega, atSix.Omega);
        Assert.NotEqual(atFive.Chi, atSix.Chi);
        Assert.NotEqual(atFive.EducationalHydraulicRadius, atSix.EducationalHydraulicRadius);
    }

    [Fact]
    public void Observed_velocity_does_not_affect_depth_hydraulic_calculation()
    {
        var depths = new[]
        {
            CreateHydraulicPoint(1, 1, IsodatSide.Left, 0m, 2m),
            CreateHydraulicPoint(2, 1, IsodatSide.Left, 4m, 6m)
        };
        var withVelocity = depths.Concat(new[]
        {
            CreateHydraulicPoint(3, 1, IsodatSide.Left, 0m, null, 999m),
            CreateHydraulicPoint(4, 1, IsodatSide.Left, 4m, null, 0.001m)
        }).ToArray();

        var depthOnly = _calculator.Calculate(
            Assert.Single(EducationalDepthProfileGrouper.Group(depths)),
            5.0);
        var withVelocityResult = _calculator.Calculate(
            Assert.Single(EducationalDepthProfileGrouper.Group(withVelocity)),
            5.0);

        Assert.Equal(depthOnly.Omega, withVelocityResult.Omega);
        Assert.Equal(depthOnly.Chi, withVelocityResult.Chi);
        Assert.Equal(depthOnly.EducationalHydraulicRadius, withVelocityResult.EducationalHydraulicRadius);
        Assert.Equal(2, withVelocityResult.PointCount);
    }

    [Fact]
    public void Calculating_one_profile_does_not_change_another_profiles_result()
    {
        var first = CreateProfile(
            CreatePoint(1, 1, IsodatSide.Left, 0m, 2m),
            CreatePoint(2, 1, IsodatSide.Left, 2m, 2m));
        var second = CreateProfile(
            CreatePoint(3, 2, IsodatSide.Right, 0m, 4m),
            CreatePoint(4, 2, IsodatSide.Right, 2m, 4m));
        var firstBefore = _calculator.Calculate(first, 5.0);

        _calculator.Calculate(second, 5.0);
        var firstAfter = _calculator.Calculate(first, 5.0);

        Assert.Equal(firstBefore.Omega, firstAfter.Omega);
        Assert.Equal(firstBefore.Chi, firstAfter.Chi);
        Assert.Equal(firstBefore.EducationalHydraulicRadius, firstAfter.EducationalHydraulicRadius);
    }

    [Fact]
    public void Zero_area_is_supported_when_profile_perimeter_is_nonzero()
    {
        var profile = CreateProfile(
            CreatePoint(1, 1, IsodatSide.Right, 0m, 0m),
            CreatePoint(2, 1, IsodatSide.Right, 2m, 0m));

        var result = _calculator.Calculate(profile, 5.0);

        Assert.Equal(0.0, result.Omega);
        Assert.Equal(2.0, result.Chi);
        Assert.Equal(0.0, result.EducationalHydraulicRadius);
    }

    [Fact]
    public void Throws_when_perimeter_is_zero_and_hydraulic_radius_is_undefined()
    {
        var profile = CreateProfile(CreatePoint(1, 1, IsodatSide.Left, 0m, 2m));

        Assert.Throws<DivideByZeroException>(() => _calculator.Calculate(profile, 5.0));
    }

    [Fact]
    public void Empty_depth_input_creates_no_profiles_or_points()
    {
        var onlyVelocity = new[]
        {
            CreateHydraulicPoint(1, 1, IsodatSide.Left, 0m, null, 0.5m)
        };

        Assert.Empty(EducationalDepthProfileGrouper.Group(Array.Empty<IsodatHydraulicPoint>()));
        Assert.Empty(EducationalDepthProfileGrouper.Group(onlyVelocity));
    }

    [Fact]
    public void Rejects_invalid_water_depth_and_invalid_profile_arguments()
    {
        var profile = CreateProfile(CreatePoint(1, 1, IsodatSide.Left, 0m, 2m));

        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(profile, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(profile, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(profile, -1.0));
        Assert.Throws<ArgumentNullException>(() => _calculator.Calculate(null!, 5.0));
        Assert.Throws<ArgumentNullException>(
            () => EducationalDepthProfileGrouper.Group(null!));
        Assert.Throws<ArgumentException>(
            () => EducationalDepthProfileGrouper.Group(new IsodatHydraulicPoint[] { null! }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EducationalDepthProfileGrouper.Group(new[]
            {
                CreateHydraulicPoint(1, 1, IsodatSide.Left, 0m, -0.1m)
            }));
    }

    private static IReadOnlyList<IsodatHydraulicPoint> CreateRealisticPointSet()
    {
        var points = new List<IsodatHydraulicPoint>();
        long id = 1;
        foreach (var section in new[] { 1, 2, 3 })
        foreach (var side in Enum.GetValues<IsodatSide>())
        {
            for (var index = 0; index < 8; index++)
                points.Add(CreateHydraulicPoint(id++, section, side, index, 1m + index % 4));
            for (var index = 0; index < 6; index++)
                points.Add(CreateHydraulicPoint(id++, section, side, index, null, 0.1m * index));
        }
        return points;
    }

    private static EducationalDepthProfile CreateProfile(params EducationalDepthPoint[] points) =>
        new(points[0].CrossSectionNumber, points[0].Side, points);

    private static EducationalDepthPoint CreatePoint(
        long sourceId,
        int section,
        IsodatSide side,
        decimal x,
        decimal depth) =>
        new(x, depth, section, side, sourceId);

    private static IsodatHydraulicPoint CreateHydraulicPoint(
        long sourceId,
        int section,
        IsodatSide side,
        decimal x,
        decimal? depth,
        decimal? velocity = null)
        => new(
            0,
            section,
            side,
            x,
            depth,
            depth.HasValue ? null : velocity ?? 0m,
            sourceId,
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            side == IsodatSide.Left ? "Левый берег" : "Правый берег",
            (int)sourceId,
            depth.HasValue ? "B" : "F");

    private static string Signature(EducationalDepthPoint point) =>
        $"{point.SourceIsodatId}|{point.CrossSectionNumber}|{point.Side}|{point.X}|{point.DepthH}";
}
