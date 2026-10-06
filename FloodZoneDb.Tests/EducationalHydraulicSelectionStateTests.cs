using FloodZoneCalculator.Domain;
using FloodZoneCalculator.Presentation.Educational;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class EducationalHydraulicSelectionStateTests
{
    [Fact]
    public void Supports_every_section_and_side_and_returns_the_matching_prepared_profile()
    {
        var data = CreateProfiles();
        var state = new EducationalHydraulicSelectionState(data);

        Assert.Equal(new[] { 1, 2, 3 }, state.CrossSectionNumbers);
        Assert.Equal(new[] { IsodatSide.Left, IsodatSide.Right }, state.Sides);
        foreach (var section in new[] { 1, 2, 3 })
        foreach (var side in Enum.GetValues<IsodatSide>())
        {
            var selected = state.Select(section, side).SelectedProfile;
            Assert.Equal(section, selected.CrossSectionNumber);
            Assert.Equal(side, selected.Side);
            Assert.Equal(8, selected.DepthPoints.Count);
            Assert.Equal(6, selected.VelocityPoints.Count);
        }
    }

    [Fact]
    public void Render_frames_keep_all_source_points_and_repeated_x_values()
    {
        var profile = Assert.Single(CreateProfiles(),
            item => item.CrossSectionNumber == 1 && item.Side == IsodatSide.Left);
        var depth = new EducationalDepthProfileRenderer().Prepare(profile);
        var velocity = new EducationalVelocityProfileRenderer().Prepare(profile);

        Assert.Equal(8, depth.Points.Count);
        Assert.Equal(7, depth.Segments.Count);
        Assert.Equal(6, velocity.Points.Count);
        Assert.Equal(5, velocity.Segments.Count);
        Assert.Equal(2, velocity.Points.Count(point => point.X == 0.0));
        Assert.Equal("X — Ri / DistanceM, учебная координата", depth.XAxisTitle);
        Assert.Equal("Y — Depth, м", depth.YAxisTitle);
        Assert.Equal(5.0, depth.ReferenceDepth);
        Assert.Equal("Учебный уровень глубины = 5.0 м", depth.ReferenceLabel);
        Assert.Equal("Y — ObservedVelocity, м/с", velocity.YAxisTitle);
    }

    [Fact]
    public void Exposes_the_precomputed_hydraulic_and_comparison_values_without_recalculation()
    {
        var profile = Assert.Single(CreateProfiles(),
            item => item.CrossSectionNumber == 2 && item.Side == IsodatSide.Right);

        Assert.Equal(8, profile.HydraulicResult.PointCount);
        Assert.Equal(6, profile.VelocityComparison.ObservedCount);
        Assert.Equal(profile.HydraulicResult.CalculatedVelocity,
            profile.VelocityComparison.CalculatedVelocity);
        Assert.Equal(
            profile.VelocityComparison.ObservedVelocityMean - profile.HydraulicResult.CalculatedVelocity,
            profile.VelocityComparison.Difference);
    }

    [Fact]
    public void Selection_and_report_generation_do_not_mutate_any_source_profile()
    {
        var data = CreateProfiles();
        var original = data.Select(Signature).ToArray();
        var state = new EducationalHydraulicSelectionState(data);
        var selected = state.Select(3, IsodatSide.Right).SelectedProfile;
        var html = new EducationalHydraulicReport().GenerateHtml(selected);
        var text = new EducationalHydraulicReport().GenerateText(selected);

        Assert.Contains(EducationalHydraulicReport.Disclaimer, html);
        Assert.Contains("V_calculated", html);
        Assert.Contains("ObservedVelocity", html);
        Assert.Contains("Гидравлический радиус R", html);
        Assert.Contains("Средняя наблюдаемая скорость", html);
        Assert.Contains("Depth", text);
        Assert.Contains("ObservedVelocity", text);
        Assert.DoesNotContain("<style>", text);
        Assert.Equal(original, data.Select(Signature));
    }

    [Fact]
    public void Rejects_missing_or_duplicate_profile_keys_and_invalid_selections()
    {
        var data = CreateProfiles();

        Assert.Throws<ArgumentException>(() =>
            new EducationalHydraulicSelectionState(data.Take(5)));
        Assert.Throws<ArgumentException>(() =>
            new EducationalHydraulicSelectionState(data.Concat(new[] { data[0] })));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EducationalHydraulicSelectionState(data, 7, IsodatSide.Left));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EducationalHydraulicSelectionState(data).Select(1, (IsodatSide)99));
        Assert.Throws<ArgumentNullException>(() =>
            new EducationalHydraulicSelectionState(null!));
    }

    private static IReadOnlyList<EducationalHydraulicProfileData> CreateProfiles()
    {
        var bundles = new List<EducationalHydraulicProfileData>();
        long id = 1;
        foreach (var section in new[] { 1, 2, 3 })
        foreach (var side in Enum.GetValues<IsodatSide>())
        {
            var points = new List<IsodatHydraulicPoint>();
            for (var index = 0; index < 8; index++)
            {
                var x = index < 2 ? 0m : index - 1m;
                points.Add(CreatePoint(id++, section, side, x, 2m + index % 3, null));
            }
            for (var index = 0; index < 6; index++)
            {
                var x = index < 2 ? 0m : index - 1m;
                points.Add(CreatePoint(id++, section, side, x, null, 0.4m + index / 10m));
            }
            var profile = Assert.Single(EducationalDepthProfileGrouper.Group(points));
            var geometry = new EducationalDepthHydraulicCalculator().Calculate(profile, 5.0);
            var hydraulic = new EducationalFullHydraulicCalculator().Calculate(
                profile,
                geometry,
                points,
                9.81,
                1.15e-6,
                5.0,
                0.001);
            var comparison = new EducationalVelocityComparisonCalculator().Calculate(hydraulic, points);
            bundles.Add(new EducationalHydraulicProfileData(profile, points, hydraulic, comparison));
        }

        return bundles;
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
            new string('b', 64),
            "Sheet1",
            checked((int)id),
            depth.HasValue ? "Depth" : "Velocity");

    private static string Signature(EducationalHydraulicProfileData profile) =>
        string.Join(";", profile.SourcePoints.Select(point =>
            $"{point.SourceIsodatId}|{point.X}|{point.DepthH}|{point.ObservedVelocity}"));
}
