using FloodZoneDb.Client;
using Npgsql;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class IsodatHydraulicPointPostgresIntegrationTests
{
    private const string SourceFileName = "Izodats_Орешко Волга_28082026 11.4.xlsx";

    [PostgreSqlFact]
    public async Task Imports_depth_and_velocity_from_existing_isodats_idempotently_without_modifying_source_rows()
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOODZONE_CONN")
            ?? throw new InvalidOperationException("FLOODZONE_CONN исчезла после discovery integration test.");
        var sourceRepository = new IsodatPostgresRepository(connectionString);
        var destinationRepository = new IsodatHydraulicPointPostgresRepository(connectionString);
        var sourceRecords = await sourceRepository.LoadAsync(sourceFileName: SourceFileName);

        Assert.Equal(360, sourceRecords.Count);
        var sourceHash = Assert.Single(sourceRecords.Select(record => record.SourceFileSha256).Distinct());
        var sourceBefore = await ReadSourceSnapshotAsync(connectionString);
        var importService = new IsodatHydraulicPointImportService(
            sourceRepository,
            destinationRepository);

        var firstImport = await importService.ImportExistingIsodatsAsync(sourceHash);
        var secondImport = await importService.ImportExistingIsodatsAsync(sourceHash);
        var points = await destinationRepository.LoadAsync(sourceHash);

        Assert.Equal(360, firstImport.SourceRecordCount);
        Assert.Equal(84, firstImport.EligibleRecordCount);
        Assert.InRange(firstImport.InsertedCount, 0, 84);
        Assert.Equal(0, secondImport.InsertedCount);
        Assert.Equal(84, points.Count);
        Assert.All(points, point =>
            Assert.Equal(IsodatHydraulicPoint.EducationalSyntheticInterpretation, point.Interpretation));
        Assert.Equal(
            sourceRecords.Where(record => record.IsodatType is IsodatType.Depth or IsodatType.Velocity)
                .Select(record => $"{record.Id}|{record.CrossSectionNumber}|{record.Side}|{record.DistanceM}|"
                                  + $"{(record.IsodatType == IsodatType.Depth ? record.Value : null)}|"
                                  + $"{(record.IsodatType == IsodatType.Velocity ? record.Value : null)}|"
                                  + $"{record.SourceSheet}|{record.SourceRow}|{record.ValueSourceColumn}")
                .OrderBy(signature => signature, StringComparer.Ordinal),
            points.Select(point => $"{point.SourceIsodatId}|{point.CrossSectionNumber}|{point.Side}|{point.X}|"
                                   + $"{point.DepthH}|{point.ObservedVelocity}|{point.SourceSheet}|"
                                   + $"{point.SourceRow}|{point.SourceValueColumn}")
                .OrderBy(signature => signature, StringComparer.Ordinal));

        var profiles = IsodatHydraulicProfileGrouper.Group(points);
        Assert.Equal(6, profiles.Count);
        Assert.Equal(new[] { 1, 2, 3 }, profiles.Select(profile => profile.CrossSectionNumber).Distinct());
        Assert.Equal(Enum.GetValues<IsodatSide>(), profiles.Select(profile => profile.Side).Distinct().OrderBy(side => side));
        foreach (var profile in profiles)
        {
            Assert.Equal(14, profile.Points.Count);
            Assert.Equal(8, profile.Points.Count(point => point.DepthH.HasValue));
            Assert.Equal(6, profile.Points.Count(point => point.ObservedVelocity.HasValue));
        }

        Assert.Equal(
            sourceBefore,
            await ReadSourceSnapshotAsync(connectionString));
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
