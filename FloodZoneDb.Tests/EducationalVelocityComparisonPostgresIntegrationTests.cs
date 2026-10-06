using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace FloodZoneDb.Tests;

public sealed class EducationalVelocityComparisonPostgresIntegrationTests
{
    private const string SourceFileName = "Izodats_Орешко Волга_28082026 11.4.xlsx";
    private readonly ITestOutputHelper _output;

    public EducationalVelocityComparisonPostgresIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [PostgreSqlFact]
    public async Task Compares_real_velocity_records_to_stage_242_results_without_modifying_isodats()
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOODZONE_CONN")
            ?? throw new InvalidOperationException("FLOODZONE_CONN исчезла после discovery integration test.");
        var sourceRepository = new IsodatPostgresRepository(connectionString);
        var pointRepository = new IsodatHydraulicPointPostgresRepository(connectionString);
        var sourceBefore = await ReadSourceSnapshotAsync(connectionString);
        var records = await sourceRepository.LoadAsync(sourceFileName: SourceFileName);
        Assert.Equal(360, records.Count);
        Assert.Equal(48, records.Count(record => record.IsodatType == IsodatType.Depth));
        Assert.Equal(36, records.Count(record => record.IsodatType == IsodatType.Velocity));

        var groupedSource = IsodatGrouper.GroupByCrossSection(records);
        var sourceVelocities = groupedSource.CrossSections
            .SelectMany(section => Enum.GetValues<IsodatSide>()
                .SelectMany(side => groupedSource.GetSet(section.CrossSectionNumber, side)!
                    .ByType[IsodatType.Velocity]))
            .ToArray();
        Assert.Equal(36, sourceVelocities.Length);

        var sourceHash = Assert.Single(records.Select(record => record.SourceFileSha256).Distinct());
        var persistedPoints = await pointRepository.LoadAsync(sourceHash);
        var persistedVelocities = persistedPoints
            .Where(point => point.ObservedVelocity.HasValue)
            .ToArray();
        Assert.Equal(84, persistedPoints.Count);
        Assert.Equal(36, persistedVelocities.Length);
        Assert.Equal(
            sourceVelocities
                .Select(record => $"{record.Id}|{record.CrossSectionNumber}|{record.Side}|{record.DistanceM}|{record.Value}")
                .OrderBy(signature => signature, StringComparer.Ordinal),
            persistedVelocities
                .Select(point => $"{point.SourceIsodatId}|{point.CrossSectionNumber}|{point.Side}|{point.X}|{point.ObservedVelocity}")
                .OrderBy(signature => signature, StringComparer.Ordinal));

        var profiles = EducationalDepthProfileGrouper.Group(persistedPoints);
        Assert.Equal(6, profiles.Count);
        var geometryCalculator = new EducationalDepthHydraulicCalculator();
        var hydraulicCalculator = new EducationalFullHydraulicCalculator();
        var comparisonCalculator = new EducationalVelocityComparisonCalculator();
        var comparisons = profiles
            .Select(profile =>
            {
                var geometry = geometryCalculator.Calculate(profile, 5.0);
                var hydraulic = hydraulicCalculator.Calculate(
                    profile,
                    geometry,
                    persistedPoints,
                    gravity: 9.81,
                    kinematicViscosity: 1.15e-6,
                    educationalWaterDepth: 5.0,
                    hydraulicSlope: 0.001);
                return comparisonCalculator.Calculate(hydraulic, persistedPoints);
            })
            .ToArray();

        Assert.Equal(6, comparisons.Length);
        Assert.Equal(36, comparisons.Sum(result => result.ObservedCount));
        Assert.All(comparisons, result =>
        {
            Assert.Equal(6, result.ObservedCount);
            Assert.True(double.IsFinite(result.ObservedVelocityMean));
            Assert.True(double.IsFinite(result.CalculatedVelocity));
            Assert.True(double.IsFinite(result.Difference));
            Assert.True(double.IsFinite(result.AbsoluteDifference));
            Assert.True(result.RelativeDifference.HasValue);
            Assert.True(result.Ratio.HasValue);
        });

        _output.WriteLine(
            "CrossSection | Side | ObservedCount | ObservedMin | ObservedMax | ObservedMean | "
            + "V_calculated | Difference | AbsoluteDifference | RelativeDifference | Ratio");
        foreach (var result in comparisons)
            _output.WriteLine(FormattableString.Invariant(
                $"{result.CrossSectionNumber} | {result.Side} | {result.ObservedCount} | {result.ObservedVelocityMin:F6} | {result.ObservedVelocityMax:F6} | {result.ObservedVelocityMean:F6} | {result.CalculatedVelocity:F6} | {result.Difference:F6} | {result.AbsoluteDifference:F6} | {result.RelativeDifference:F6} | {result.Ratio:F6}"));

        Assert.Equal(sourceBefore, await ReadSourceSnapshotAsync(connectionString));
    }

    private static async Task<string> ReadSourceSnapshotAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand("""
            select count(*)::bigint,
                   md5(coalesce(string_agg(md5(to_jsonb(t)::text), '' order by to_jsonb(t)::text), ''))
            from isodats as t
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Не удалось получить контрольный снимок исходных изодат.");
        return $"{reader.GetInt64(0)}:{reader.GetString(1)}";
    }
}
