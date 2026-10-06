using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class EducationalFullHydraulicCalculatorTests
{
    private const double G = 9.81;
    private const double Nu = 1.15e-6;
    private const double H = 5.0;
    private const double If = 0.001;
    private readonly EducationalFullHydraulicCalculator _calculator = new();

    [Fact]
    public void Calculates_the_complete_educational_chain_for_six_profiles_and_realistic_record_counts()
    {
        var points = CreatePointSet();
        var profiles = EducationalDepthProfileGrouper.Group(points);
        var results = profiles
            .Select(profile => Calculate(profile, points))
            .ToArray();

        Assert.Equal(48, points.Count(point => point.DepthH.HasValue));
        Assert.Equal(36, points.Count(point => point.ObservedVelocity.HasValue));
        Assert.Equal(6, results.Length);
        Assert.All(results, result =>
        {
            Assert.Equal(8, result.PointCount);
            Assert.True(result.B > 0.0);
            Assert.Equal(result.MaxX - result.MinX, result.B);
            Assert.Equal(result.Omega / result.Chi, result.HydraulicRadius, 12);
            Assert.Equal(Math.Sqrt(G * result.HydraulicRadius * If), result.ShearVelocity, 12);
            Assert.Equal(
                GrishaninChezyCalculator.Calculate(G, Nu, H, result.B),
                result.ChezyCoefficient,
                12);
            Assert.Equal(
                result.ChezyCoefficient * Math.Sqrt(result.HydraulicRadius * If),
                result.CalculatedVelocity,
                12);
            Assert.Equal(result.CalculatedVelocity * result.Omega, result.Discharge, 12);
        });
        Assert.All(results, result =>
            Assert.Equal(IsodatHydraulicPoint.EducationalSyntheticInterpretation, result.Interpretation));
    }

    [Fact]
    public void Changing_slope_changes_shear_velocity_calculated_velocity_and_discharge()
    {
        var (profile, points) = CreateSingleProfile();

        var baseline = Calculate(profile, points);
        var changed = Calculate(profile, points, hydraulicSlope: If * 4.0);

        Assert.NotEqual(baseline.ShearVelocity, changed.ShearVelocity);
        Assert.NotEqual(baseline.CalculatedVelocity, changed.CalculatedVelocity);
        Assert.NotEqual(baseline.Discharge, changed.Discharge);
        Assert.Equal(baseline.Omega, changed.Omega);
        Assert.Equal(baseline.Chi, changed.Chi);
        Assert.Equal(baseline.ChezyCoefficient, changed.ChezyCoefficient);
    }

    [Fact]
    public void Changing_depth_changes_chezy_velocity_and_discharge_without_requiring_geometry_to_change()
    {
        var (profile, points) = CreateSingleProfile();

        var baseline = Calculate(profile, points);
        var changed = Calculate(profile, points, educationalWaterDepth: H + 1.0);

        Assert.Equal(baseline.Omega, changed.Omega);
        Assert.Equal(baseline.Chi, changed.Chi);
        Assert.Equal(baseline.HydraulicRadius, changed.HydraulicRadius);
        Assert.NotEqual(baseline.ChezyCoefficient, changed.ChezyCoefficient);
        Assert.NotEqual(baseline.CalculatedVelocity, changed.CalculatedVelocity);
        Assert.NotEqual(baseline.Discharge, changed.Discharge);
    }

    [Fact]
    public void Changing_width_changes_chezy_coefficient_velocity_and_discharge()
    {
        var (profile, points) = CreateSingleProfile();

        var baseline = Calculate(profile, points);
        var movedPoints = points.Select(point => point.DepthH.HasValue
            ? CreatePoint(
                point.SourceIsodatId,
                point.CrossSectionNumber,
                point.Side,
                point.X * 2m,
                point.DepthH,
                null)
            : CreatePoint(
                point.SourceIsodatId,
                point.CrossSectionNumber,
                point.Side,
                point.X,
                null,
                point.ObservedVelocity)).ToArray();
        var movedProfile = Assert.Single(EducationalDepthProfileGrouper.Group(movedPoints));

        var changed = Calculate(movedProfile, movedPoints);

        Assert.Equal(baseline.Omega * 2.0, changed.Omega, 10);
        Assert.NotEqual(baseline.B, changed.B);
        Assert.NotEqual(baseline.ChezyCoefficient, changed.ChezyCoefficient);
        Assert.NotEqual(baseline.CalculatedVelocity, changed.CalculatedVelocity);
        Assert.NotEqual(baseline.Discharge, changed.Discharge);
    }

    [Fact]
    public void Observed_velocity_is_reported_separately_and_does_not_affect_calculated_velocity()
    {
        var (profile, points) = CreateSingleProfile();
        var changedObservations = points.Select(point => point.ObservedVelocity.HasValue
            ? CreatePoint(
                point.SourceIsodatId,
                point.CrossSectionNumber,
                point.Side,
                point.X,
                null,
                point.ObservedVelocity.Value * 100m)
            : point).ToArray();

        var baseline = Calculate(profile, points);
        var changed = Calculate(profile, changedObservations);

        Assert.Equal(baseline.CalculatedVelocity, changed.CalculatedVelocity);
        Assert.Equal(baseline.Discharge, changed.Discharge);
        Assert.Equal(baseline.ObservedVelocityMin * 100.0, changed.ObservedVelocityMin);
        Assert.Equal(baseline.ObservedVelocityMax * 100.0, changed.ObservedVelocityMax);
    }

    [Fact]
    public void Profiles_are_independent_and_repeated_calculations_are_deterministic()
    {
        var points = CreatePointSet();
        var profiles = EducationalDepthProfileGrouper.Group(points);
        var firstProfile = profiles[0];

        var first = Calculate(firstProfile, points);
        Calculate(profiles[1], points);
        var repeated = Calculate(firstProfile, points);

        Assert.Equal(first.Omega, repeated.Omega);
        Assert.Equal(first.Chi, repeated.Chi);
        Assert.Equal(first.B, repeated.B);
        Assert.Equal(first.HydraulicRadius, repeated.HydraulicRadius);
        Assert.Equal(first.ShearVelocity, repeated.ShearVelocity);
        Assert.Equal(first.ChezyCoefficient, repeated.ChezyCoefficient);
        Assert.Equal(first.CalculatedVelocity, repeated.CalculatedVelocity);
        Assert.Equal(first.Discharge, repeated.Discharge);
    }

    [Fact]
    public void Rejects_nonpositive_width_before_calculating_profile()
    {
        var points = new[]
        {
            CreatePoint(1, 1, IsodatSide.Left, 3m, 2m, null),
            CreatePoint(2, 1, IsodatSide.Left, 3m, 4m, null),
            CreatePoint(3, 1, IsodatSide.Left, 0m, null, 0.5m)
        };
        var profile = Assert.Single(EducationalDepthProfileGrouper.Group(points));

        var exception = Assert.Throws<InvalidOperationException>(
            () => Calculate(profile, points));

        Assert.Contains("B", exception.Message);
    }

    [Fact]
    public void Rejects_missing_observed_velocity_for_the_profile()
    {
        var points = new[]
        {
            CreatePoint(1, 1, IsodatSide.Left, 0m, 2m, null),
            CreatePoint(2, 1, IsodatSide.Left, 2m, 3m, null)
        };
        var profile = Assert.Single(EducationalDepthProfileGrouper.Group(points));

        Assert.Throws<InvalidOperationException>(
            () => Calculate(profile, points));
    }

    private static (EducationalDepthProfile Profile, IReadOnlyList<IsodatHydraulicPoint> Points)
        CreateSingleProfile()
    {
        var points = new[]
        {
            CreatePoint(1, 1, IsodatSide.Left, 0m, 2m, null),
            CreatePoint(2, 1, IsodatSide.Left, 1m, 3m, null),
            CreatePoint(3, 1, IsodatSide.Left, 2m, 4m, null),
            CreatePoint(4, 1, IsodatSide.Left, 0m, null, 0.2m),
            CreatePoint(5, 1, IsodatSide.Left, 2m, null, 0.4m)
        };
        return (Assert.Single(EducationalDepthProfileGrouper.Group(points)), points);
    }

    private static IReadOnlyList<IsodatHydraulicPoint> CreatePointSet()
    {
        var points = new List<IsodatHydraulicPoint>();
        long id = 1;
        foreach (var section in new[] { 1, 2, 3 })
        foreach (var side in Enum.GetValues<IsodatSide>())
        {
            for (var index = 0; index < 8; index++)
                points.Add(CreatePoint(id++, section, side, index, 1m + index % 4, null));
            for (var index = 0; index < 6; index++)
                points.Add(CreatePoint(id++, section, side, index, null, 0.1m * (index + 1)));
        }

        return points;
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

    private EducationalFullHydraulicResult Calculate(
        EducationalDepthProfile profile,
        IReadOnlyCollection<IsodatHydraulicPoint> points,
        double educationalWaterDepth = H,
        double hydraulicSlope = If)
    {
        var geometry = new EducationalDepthHydraulicCalculator()
            .Calculate(profile, educationalWaterDepth);
        return _calculator.Calculate(
            profile,
            geometry,
            points,
            G,
            Nu,
            educationalWaterDepth,
            hydraulicSlope);
    }
}
