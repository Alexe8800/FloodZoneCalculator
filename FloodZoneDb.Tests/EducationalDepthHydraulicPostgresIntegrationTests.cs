using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace FloodZoneDb.Tests;

public sealed class EducationalDepthHydraulicPostgresIntegrationTests
{
    private const string SourceFileName = "Izodats_Орешко Волга_28082026 11.4.xlsx";
    private readonly ITestOutputHelper _output;

    public EducationalDepthHydraulicPostgresIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [PostgreSqlFact]
    public async Task Reads_and_calculates_six_real_depth_profiles_without_modifying_isodats()
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOODZONE_CONN")
            ?? throw new InvalidOperationException("FLOODZONE_CONN исчезла после discovery integration test.");
        var sourceRepository = new IsodatPostgresRepository(connectionString);
        var pointRepository = new IsodatHydraulicPointPostgresRepository(connectionString);
        var before = await ReadSourceSnapshotAsync(connectionString);
        var records = await sourceRepository.LoadAsync(sourceFileName: SourceFileName);

        Assert.Equal(360, records.Count);
        Assert.Equal(48, records.Count(record => record.IsodatType == IsodatType.Depth));
        Assert.Equal(36, records.Count(record => record.IsodatType == IsodatType.Velocity));
        var groupedSource = IsodatGrouper.GroupByCrossSection(records);
        Assert.Equal(new[] { 1, 2, 3 }, groupedSource.CrossSections
            .Select(section => section.CrossSectionNumber));
        Assert.Equal(6, groupedSource.CrossSections.Count * 2);
        foreach (var section in new[] { 1, 2, 3 })
        foreach (var side in Enum.GetValues<IsodatSide>())
            Assert.Equal(8, groupedSource.GetSet(section, side)!.ByType[IsodatType.Depth].Count);

        var sourceHash = Assert.Single(records.Select(record => record.SourceFileSha256).Distinct());
        var persistedPoints = await pointRepository.LoadAsync(sourceHash);
        Assert.Equal(84, persistedPoints.Count);
        Assert.Equal(48, persistedPoints.Count(point => point.DepthH.HasValue));
        Assert.Equal(36, persistedPoints.Count(point => point.ObservedVelocity.HasValue));

        var depthSourceIds = groupedSource.CrossSections
            .SelectMany(section => Enum.GetValues<IsodatSide>()
                .SelectMany(side => GetDepthSet(groupedSource, section.CrossSectionNumber, side)))
            .Select(record => record.Id)
            .OrderBy(id => id)
            .ToArray();
        var persistedDepthSourceIds = persistedPoints
            .Where(point => point.DepthH.HasValue)
            .Select(point => point.SourceIsodatId)
            .OrderBy(id => id)
            .ToArray();
        Assert.Equal(depthSourceIds, persistedDepthSourceIds);

        var profiles = EducationalDepthProfileGrouper.Group(persistedPoints);
        Assert.Equal(6, profiles.Count);
        var depthCalculator = new EducationalDepthHydraulicCalculator();
        var calculator = new EducationalFullHydraulicCalculator();
        var results = profiles
            .Select(profile =>
            {
                var geometry = depthCalculator.Calculate(profile, 5.0);
                return calculator.Calculate(
                    profile,
                    geometry,
                    persistedPoints,
                    gravity: 9.81,
                    kinematicViscosity: 1.15e-6,
                    educationalWaterDepth: 5.0,
                    hydraulicSlope: 0.001);
            })
            .ToArray();

        Assert.All(results, result =>
        {
            Assert.Equal(8, result.PointCount);
            Assert.True(result.B > 0.0);
            Assert.Equal(result.MaxX - result.MinX, result.B);
            Assert.Equal(5.0, result.EducationalWaterDepth);
            Assert.True(double.IsFinite(result.Omega));
            Assert.True(double.IsFinite(result.Chi));
            Assert.True(double.IsFinite(result.HydraulicRadius));
            Assert.True(double.IsFinite(result.ShearVelocity));
            Assert.True(double.IsFinite(result.ChezyCoefficient));
            Assert.True(double.IsFinite(result.CalculatedVelocity));
            Assert.True(double.IsFinite(result.Discharge));
            Assert.True(result.Chi > 0.0);
        });
        foreach (var result in results)
            _output.WriteLine(FormattableString.Invariant(
                $"{result.CrossSectionNumber} | {result.Side} | {result.PointCount} | {result.MinX:F6} | {result.MaxX:F6} | {result.B:F6} | {result.Omega:F6} | {result.Chi:F6} | {result.HydraulicRadius:F6} | {result.ShearVelocity:F6} | {result.ChezyCoefficient:F6} | {result.CalculatedVelocity:F6} | {result.Discharge:F6} | {result.ObservedVelocityMin:F6} | {result.ObservedVelocityMax:F6}"));

        var after = await ReadSourceSnapshotAsync(connectionString);
        Assert.Equal(before, after);
    }

    private static IReadOnlyList<IsodatRecord> GetDepthSet(
        IsodatGrouping grouping,
        int crossSectionNumber,
        IsodatSide side) =>
        grouping.GetSet(crossSectionNumber, side)!.ByType[IsodatType.Depth];

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
