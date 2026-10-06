using FloodZoneCalculator.Domain;
using FloodZoneCalculator.Presentation.Educational;
using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class EducationalHydraulicSummaryModelTests
{
    [Fact]
    public void Accepts_six_profiles_and_orders_them_by_section_then_side()
    {
        var input = CreateProfiles()
            .OrderByDescending(profile => profile.CrossSectionNumber)
            .ThenByDescending(profile => profile.Side)
            .ToArray();
        var original = input.ToArray();
        var model = new EducationalHydraulicSummaryModel(input);

        Assert.Equal(new[]
        {
            (1, IsodatSide.Left), (1, IsodatSide.Right),
            (2, IsodatSide.Left), (2, IsodatSide.Right),
            (3, IsodatSide.Left), (3, IsodatSide.Right)
        }, model.Rows.Select(row => (row.CrossSectionNumber, row.Side)));
        Assert.Equal(original, input);
        Assert.Equal(6, model.Profiles.Count);
    }

    [Fact]
    public void Preserves_all_hydraulic_and_velocity_comparison_values_without_rounding()
    {
        var profiles = CreateProfiles();
        var model = new EducationalHydraulicSummaryModel(profiles);

        foreach (var row in model.Rows)
        {
            var hydraulic = row.Profile.HydraulicResult;
            var comparison = row.Profile.VelocityComparison;
            Assert.Equal(8, row.DepthCount);
            Assert.Equal(6, row.VelocityCount);
            Assert.Equal(hydraulic.Omega, row.Omega);
            Assert.Equal(hydraulic.Chi, row.Chi);
            Assert.Equal(hydraulic.HydraulicRadius, row.HydraulicRadius);
            Assert.Equal(hydraulic.B, row.B);
            Assert.Equal(hydraulic.ShearVelocity, row.ShearVelocity);
            Assert.Equal(hydraulic.ChezyCoefficient, row.ChezyCoefficient);
            Assert.Equal(hydraulic.CalculatedVelocity, row.CalculatedVelocity);
            Assert.Equal(hydraulic.Discharge, row.Discharge);
            Assert.Equal(comparison.ObservedVelocityMean, row.ObservedVelocityMean);
            Assert.Equal(comparison.Difference, row.Difference);
            Assert.Equal(comparison.RelativeDifference, row.RelativeDifference);
            Assert.Equal(comparison.Ratio, row.Ratio);
        }
    }

    [Fact]
    public void Chart_render_models_keep_profile_order_and_use_existing_values()
    {
        var model = new EducationalHydraulicSummaryModel(CreateProfiles());
        var render = new EducationalHydraulicSummaryRenderModels(model);

        Assert.Equal(2, render.VelocityComparison.Count);
        Assert.Equal("ObservedVelocityMean", render.VelocityComparison[0].Name);
        Assert.Equal("V_calculated", render.VelocityComparison[1].Name);
        Assert.Equal(model.Rows.Select(row => $"{row.CrossSectionNumber} {row.Side}"),
            render.VelocityComparison[0].Points.Select(point => point.ProfileLabel));
        Assert.Equal(model.Rows.Select(row => row.ObservedVelocityMean),
            render.VelocityComparison[0].Points.Select(point => point.Value));
        Assert.Equal(model.Rows.Select(row => row.CalculatedVelocity),
            render.VelocityComparison[1].Points.Select(point => point.Value));
        Assert.Equal(model.Rows.Select(row => row.Discharge),
            render.Discharge.Single().Points.Select(point => point.Value));
        Assert.Equal(model.Rows.Select(row => row.HydraulicRadius),
            render.HydraulicRadius.Single().Points.Select(point => point.Value));
    }

    [Fact]
    public void Selection_returns_the_requested_section_and_side_and_rejects_missing_profiles()
    {
        var model = new EducationalHydraulicSummaryModel(CreateProfiles());
        var selection = new EducationalHydraulicSummarySelectionState(model)
            .Select(2, IsodatSide.Right);

        Assert.Equal(2, selection.SelectedProfile.CrossSectionNumber);
        Assert.Equal(IsodatSide.Right, selection.SelectedProfile.Side);
        Assert.Throws<KeyNotFoundException>(() =>
            model.GetProfile(4, IsodatSide.Left));
        Assert.Throws<KeyNotFoundException>(() =>
            selection.Select(4, IsodatSide.Left));
    }

    [Fact]
    public void Rejects_null_empty_incomplete_and_null_element_inputs()
    {
        var profiles = CreateProfiles();

        Assert.Throws<ArgumentNullException>(() =>
            new EducationalHydraulicSummaryModel(null));
        Assert.Throws<ArgumentException>(() =>
            new EducationalHydraulicSummaryModel(Array.Empty<EducationalHydraulicProfileData>()));
        Assert.Throws<ArgumentException>(() =>
            new EducationalHydraulicSummaryModel(profiles.Take(5)));
        Assert.Throws<ArgumentException>(() =>
            new EducationalHydraulicSummaryModel(profiles.Concat(new[] { profiles[0] })));
        Assert.Throws<ArgumentException>(() =>
            new EducationalHydraulicSummaryModel(new EducationalHydraulicProfileData[] { null! }));
        Assert.Throws<ArgumentNullException>(() =>
            new EducationalHydraulicSummarySelectionState(null!));
    }

    [Fact]
    public void Rebuilding_summary_is_deterministic_and_does_not_change_the_input_collection()
    {
        var input = CreateProfiles().Reverse().ToList();
        var before = input.Select(ProfileSignature).ToArray();

        var first = new EducationalHydraulicSummaryModel(input);
        var second = new EducationalHydraulicSummaryModel(input);

        Assert.Equal(first.Rows.Select(RowSignature), second.Rows.Select(RowSignature));
        Assert.Equal(before, input.Select(ProfileSignature));
    }

    [Fact]
    public void Summary_report_contains_all_profiles_results_parameters_and_disclaimer()
    {
        var summary = new EducationalHydraulicSummaryModel(CreateProfiles());
        var report = new EducationalHydraulicReport();
        var html = report.GenerateSummaryHtml(summary);
        var text = report.GenerateSummaryText(summary);

        Assert.Contains("Учебный синтетический гидравлический расчёт — сводка", html);
        Assert.Contains(EducationalHydraulicReport.Disclaimer, html);
        Assert.Contains(EducationalHydraulicReport.Disclaimer, text);
        Assert.Contains("ObservedVelocityMean", html);
        Assert.Contains("V_calculated", html);
        Assert.Contains("EducationalWaterDepth", html);
        Assert.Contains("nu", html);
        Assert.Contains("RelativeDifference", text);
        Assert.Contains("3 Right", text);
        Assert.Equal(6, text.Split(new[] { "\n" }, StringSplitOptions.None)
            .Count(line => line.StartsWith("1\t", StringComparison.Ordinal)
                || line.StartsWith("2\t", StringComparison.Ordinal)
                || line.StartsWith("3\t", StringComparison.Ordinal)));
    }

    private static IReadOnlyList<EducationalHydraulicProfileData> CreateProfiles()
    {
        var profiles = new List<EducationalHydraulicProfileData>();
        long id = 1;
        foreach (var section in new[] { 1, 2, 3 })
        foreach (var side in new[] { IsodatSide.Left, IsodatSide.Right })
        {
            var source = new List<IsodatHydraulicPoint>();
            for (var index = 0; index < 8; index++)
            {
                var x = index < 2 ? 0m : index - 1m;
                source.Add(CreatePoint(id++, section, side, x, 2m + index % 3, null));
            }
            for (var index = 0; index < 6; index++)
            {
                var x = index < 2 ? 0m : index - 1m;
                source.Add(CreatePoint(id++, section, side, x, null, 0.4m + index / 10m));
            }

            var depthProfile = Assert.Single(EducationalDepthProfileGrouper.Group(source));
            var geometry = new EducationalDepthHydraulicCalculator().Calculate(depthProfile, 5.0);
            var hydraulic = new EducationalFullHydraulicCalculator().Calculate(
                depthProfile, geometry, source, 9.81, 1.15e-6, 5.0, 0.001);
            var comparison = new EducationalVelocityComparisonCalculator().Calculate(hydraulic, source);
            profiles.Add(new EducationalHydraulicProfileData(
                depthProfile, source, hydraulic, comparison));
        }

        return profiles;
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
            new string('c', 64),
            "Sheet1",
            checked((int)id),
            depth.HasValue ? "Depth" : "Velocity");

    private static string ProfileSignature(EducationalHydraulicProfileData profile) =>
        $"{profile.CrossSectionNumber}|{profile.Side}|"
        + string.Join(";", profile.SourcePoints.Select(point =>
            $"{point.SourceIsodatId}:{point.X}:{point.DepthH}:{point.ObservedVelocity}"));

    private static string RowSignature(EducationalHydraulicSummaryRow row) =>
        $"{row.CrossSectionNumber}|{row.Side}|{row.Omega:G17}|{row.Chi:G17}|"
        + $"{row.CalculatedVelocity:G17}|{row.Discharge:G17}|{row.ObservedVelocityMean:G17}|"
        + $"{row.Difference:G17}|{row.RelativeDifference:G17}|{row.Ratio:G17}";
}
