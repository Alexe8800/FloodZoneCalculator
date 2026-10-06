using System.Security.Cryptography;
using System.Text;
using FloodZoneDb.Client;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace FloodZoneDb.Tests;

public sealed class IsodatPostgresIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public IsodatPostgresIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly string WorkbookPath = Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "Izodats_Орешко Волга_28082026 11.4.xlsx");

    [PostgreSqlFact]
    public async Task Import_round_trips_and_reimport_does_not_duplicate_or_touch_calculation_tables()
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOODZONE_CONN")
            ?? throw new InvalidOperationException("FLOODZONE_CONN исчезла после discovery integration test.");
        var repository = new IsodatPostgresRepository(connectionString);
        var importer = new IsodatExcelImportService(repository);
        var parsed = importer.ReadRecords(WorkbookPath);
        var testHash = CreateIsolatedTestHash(parsed[0].SourceFileSha256);
        var records = parsed.Select(record => CopyWithHash(record, testHash)).ToArray();

        await repository.EnsureSchemaAsync();
        var beforeCounts = await ReadExistingCoreTableCountsAsync(connectionString);
        var beforeIsodatSources = await ReadIsodatSourceCountsAsync(connectionString);
        try
        {
            Assert.Equal(records.Length, await repository.InsertAsync(records));
            Assert.Equal(0, await repository.InsertAsync(records));

            var loaded = await repository.LoadBySourceHashAsync(testHash);
            Assert.Equal(records.Length, loaded.Count);
            Assert.Equal(
                records.Select(Signature).OrderBy(signature => signature, StringComparer.Ordinal),
                loaded.Select(Signature).OrderBy(signature => signature, StringComparer.Ordinal));

            var afterCounts = await ReadExistingCoreTableCountsAsync(connectionString);
            Assert.Equal(
                beforeCounts.OrderBy(pair => pair.Key),
                afterCounts.OrderBy(pair => pair.Key));
        }
        finally
        {
            await DeleteTestRowsAsync(connectionString, testHash);
            Assert.Empty(await repository.LoadBySourceHashAsync(testHash));
            var afterIsodatSources = await ReadIsodatSourceCountsAsync(connectionString);
            Assert.Equal(
                beforeIsodatSources.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                afterIsodatSources.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        }
    }

    [PostgreSqlFact]
    public async Task Read_only_load_returns_filtered_excel_records_without_changing_any_table()
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOODZONE_CONN")
            ?? throw new InvalidOperationException("FLOODZONE_CONN исчезла после discovery integration test.");
        var repository = new IsodatPostgresRepository(connectionString);
        var importer = new IsodatExcelImportService(repository);
        var parsed = importer.ReadRecords(WorkbookPath);
        var testHash = CreateIsolatedTestHash(parsed[0].SourceFileSha256);
        var records = parsed.Select(record => CopyWithHash(record, testHash)).ToArray();

        await repository.EnsureSchemaAsync();
        var beforeSetup = await ReadTableSnapshotsAsync(connectionString);
        try
        {
            Assert.Equal(records.Length, await repository.InsertAsync(records));
            var afterSetup = await ReadTableSnapshotsAsync(connectionString);

            var loaded = await repository.LoadAsync(sourceFileSha256: testHash);
            Assert.Equal(records.Length, loaded.Count);
            Assert.Equal(
                records.Select(Signature).OrderBy(signature => signature, StringComparer.Ordinal),
                loaded.Select(Signature).OrderBy(signature => signature, StringComparer.Ordinal));

            var secondRead = await repository.LoadAsync(sourceFileSha256: testHash);
            Assert.Equal(
                loaded.Select(Signature).OrderBy(signature => signature, StringComparer.Ordinal),
                secondRead.Select(Signature).OrderBy(signature => signature, StringComparer.Ordinal));

            Assert.Equal(180, (await repository.LoadAsync(
                sourceFileSha256: testHash,
                sourceSheet: "Левый берег")).Count);
            Assert.Equal(120, (await repository.LoadAsync(
                sourceFileSha256: testHash,
                crossSectionNumber: 1)).Count);
            Assert.Equal(180, (await repository.LoadAsync(
                sourceFileSha256: testHash,
                side: IsodatSide.Left)).Count);
            Assert.Equal(36, (await repository.LoadAsync(
                sourceFileSha256: testHash,
                isodatType: IsodatType.Velocity)).Count);
            Assert.Equal(6, (await repository.LoadAsync(
                sourceFileSha256: testHash,
                sourceSheet: "Левый берег",
                crossSectionNumber: 1,
                side: IsodatSide.Left,
                isodatType: IsodatType.Velocity)).Count);
            Assert.Equal(360, (await repository.LoadAsync(
                sourceFileName: records[0].SourceFileName,
                sourceFileSha256: testHash)).Count);
            Assert.Empty(await repository.LoadAsync(
                sourceFileSha256: CreateIsolatedTestHash(parsed[0].SourceFileSha256)));

            Assert.Equal(
                afterSetup.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                (await ReadTableSnapshotsAsync(connectionString)).OrderBy(
                    pair => pair.Key,
                    StringComparer.Ordinal));
        }
        finally
        {
            Assert.Equal(records.Length, await DeleteTestRowsAsync(connectionString, testHash));
            Assert.Empty(await repository.LoadAsync(sourceFileSha256: testHash));
            Assert.Equal(
                beforeSetup.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                (await ReadTableSnapshotsAsync(connectionString)).OrderBy(
                    pair => pair.Key,
                    StringComparer.Ordinal));
        }
    }

    [PostgreSqlFact]
    public async Task Restore_real_workbook_dataset_through_existing_import_pipeline()
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOODZONE_CONN")
            ?? throw new InvalidOperationException("FLOODZONE_CONN исчезла после discovery integration test.");
        var repository = new IsodatPostgresRepository(connectionString);
        var importer = new IsodatExcelImportService(repository);
        var sourceRecords = importer.ReadRecords(WorkbookPath);
        var sourceHash = sourceRecords[0].SourceFileSha256;

        await repository.EnsureSchemaAsync();
        var beforeCoreTables = await ReadCoreTableSnapshotsAsync(connectionString);
        var existing = await repository.LoadAsync(sourceFileSha256: sourceHash);
        if (existing.Count < sourceRecords.Count)
            await importer.ImportAsync(WorkbookPath);

        var loaded = await repository.LoadAsync(sourceFileSha256: sourceHash);
        Assert.Equal(360, sourceRecords.Count);
        Assert.Equal(sourceRecords.Count, loaded.Count);
        Assert.Equal(
            sourceRecords.Select(Signature).OrderBy(signature => signature, StringComparer.Ordinal),
            loaded.Select(Signature).OrderBy(signature => signature, StringComparer.Ordinal));
        Assert.Equal(
            beforeCoreTables.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            (await ReadCoreTableSnapshotsAsync(connectionString)).OrderBy(
                pair => pair.Key,
                StringComparer.Ordinal));
    }

    [PostgreSqlFact]
    public async Task Imported_excel_records_can_be_read_and_grouped_without_database_changes()
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOODZONE_CONN")
            ?? throw new InvalidOperationException("FLOODZONE_CONN исчезла после discovery integration test.");
        var repository = new IsodatPostgresRepository(connectionString);
        var importer = new IsodatExcelImportService(repository);
        var sourceHash = importer.ReadRecords(WorkbookPath)[0].SourceFileSha256;
        var before = await ReadTableSnapshotsAsync(connectionString);

        var records = await repository.LoadAsync(sourceFileSha256: sourceHash);
        var grouping = IsodatGrouper.GroupByCrossSection(records);
        var audit = IsodatDataAuditor.Audit(grouping);

        Assert.Equal(360, records.Count);
        Assert.Equal(360, audit.TotalCount);
        Assert.Equal(new[] { 1, 2, 3 }, grouping.CrossSections.Select(section => section.CrossSectionNumber));
        Assert.Equal(
            records.Select(Signature).OrderBy(signature => signature, StringComparer.Ordinal),
            GroupingRecords(grouping)
                .Select(Signature)
                .OrderBy(signature => signature, StringComparer.Ordinal));
        foreach (var section in grouping.CrossSections)
        {
            Assert.NotNull(section.Left);
            Assert.NotNull(section.Right);
            Assert.Equal(60, CountRecords(section.Left));
            Assert.Equal(60, CountRecords(section.Right));
            Assert.Equal(8, section.Left.ByType[IsodatType.Depth].Count);
            Assert.Equal(6, section.Left.ByType[IsodatType.Velocity].Count);
            Assert.Equal(23, section.Left.ByType[IsodatType.CalculatedFloodDuration].Count);
            Assert.Equal(23, section.Left.ByType[IsodatType.ActualFloodDuration].Count);
            Assert.Equal(8, section.Right.ByType[IsodatType.Depth].Count);
            Assert.Equal(6, section.Right.ByType[IsodatType.Velocity].Count);
            Assert.Equal(23, section.Right.ByType[IsodatType.CalculatedFloodDuration].Count);
            Assert.Equal(23, section.Right.ByType[IsodatType.ActualFloodDuration].Count);

            foreach (var (side, expectedSheet) in new[]
                     {
                         (IsodatSide.Left, "Левый берег"),
                         (IsodatSide.Right, "Правый берег")
                     })
            {
                var set = Assert.IsType<IsodatSideIsodats>(
                    grouping.GetSet(section.CrossSectionNumber, side));
                Assert.Equal(side, set.Side);
                Assert.Equal(
                    records.Where(record =>
                            record.CrossSectionNumber == section.CrossSectionNumber
                            && record.Side == side)
                        .Select(Signature)
                        .OrderBy(signature => signature, StringComparer.Ordinal),
                    set.ByType.Values.SelectMany(values => values)
                        .Select(Signature)
                        .OrderBy(signature => signature, StringComparer.Ordinal));
                Assert.All(
                    set.ByType.Values.SelectMany(values => values),
                    record =>
                    {
                        Assert.Equal(section.CrossSectionNumber, record.CrossSectionNumber);
                        Assert.Equal(side, record.Side);
                        Assert.Equal(expectedSheet, record.SourceSheet);
                    });
            }
        }

        Assert.Equal(24, audit.Rows.Count);
        foreach (var type in Enum.GetValues<IsodatType>())
        {
            var expectedCount = type switch
            {
                IsodatType.Depth => 8,
                IsodatType.Velocity => 6,
                IsodatType.CalculatedFloodDuration => 23,
                IsodatType.ActualFloodDuration => 23,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
            Assert.Equal(
                6 * expectedCount,
                Assert.Single(audit.TypeTotals, total => total.IsodatType == type).Count);
        }

        _output.WriteLine(
            "CrossSection | Side | Type | Count | MinValue | MaxValue | MinDistance | MaxDistance | UniqueValues | UniqueDistances");
        foreach (var row in audit.Rows)
        {
            _output.WriteLine(
                $"{row.CrossSectionNumber} | {row.Side} | {row.IsodatType} | {row.Count} | "
                + $"{row.MinValue} | {row.MaxValue} | {row.MinDistanceM} | {row.MaxDistanceM} | "
                + $"{row.UniqueValues} | {row.UniqueDistances}");
        }

        _output.WriteLine("Type totals:");
        foreach (var total in audit.TypeTotals)
        {
            _output.WriteLine(
                $"{total.IsodatType} | {total.Count} | {total.MinValue} | {total.MaxValue} | "
                + $"{total.MinDistanceM} | {total.MaxDistanceM} | {total.UniqueValues} | "
                + $"{total.UniqueDistances}");
        }

        Assert.Equal(
            before.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            (await ReadTableSnapshotsAsync(connectionString)).OrderBy(
                pair => pair.Key,
                StringComparer.Ordinal));
    }

    private static int CountRecords(IsodatSideIsodats? side) =>
        side?.ByType.Values.Sum(records => records.Count) ?? 0;

    private static IEnumerable<IsodatRecord> GroupingRecords(IsodatGrouping grouping) =>
        grouping.CrossSections
            .SelectMany(section => new[] { section.Left, section.Right })
            .Where(side => side != null)
            .SelectMany(side => side!.ByType.Values.SelectMany(records => records));

    private static async Task<IReadOnlyDictionary<string, string>> ReadCoreTableSnapshotsAsync(
        string connectionString)
    {
        var snapshots = await ReadTableSnapshotsAsync(connectionString);
        return snapshots
            .Where(pair => pair.Key is "cross_sections" or "bank_points" or "calculation_inputs")
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadTableSnapshotsAsync(
        string connectionString)
    {
        var tableNames = new[] { "isodats", "cross_sections", "bank_points", "calculation_inputs" };
        var snapshots = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var tableName in tableNames)
        {
            using var exists = new NpgsqlCommand("select to_regclass(@table_name) is not null;", connection);
            exists.Parameters.AddWithValue("table_name", "public." + tableName);
            if (!(bool)(await exists.ExecuteScalarAsync() ?? false))
            {
                snapshots[tableName] = "missing";
                continue;
            }

            using var snapshot = new NpgsqlCommand(
                $"select count(*)::bigint, md5(coalesce(string_agg(md5(to_jsonb(t)::text), '' order by to_jsonb(t)::text), '')) from public.{tableName} as t;",
                connection);
            await using var reader = await snapshot.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException($"Не удалось прочитать контрольный снимок таблицы {tableName}.");
            snapshots[tableName] = $"{reader.GetInt64(0)}:{reader.GetString(1)}";
        }

        return snapshots;
    }

    private static async Task<IReadOnlyDictionary<string, long>> ReadIsodatSourceCountsAsync(
        string connectionString)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand("""
            select source_file_sha256, count(*)
            from isodats
            group by source_file_sha256;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            counts.Add(reader.GetString(0).Trim(), reader.GetInt64(1));
        return counts;
    }

    private static async Task<IReadOnlyDictionary<string, long>> ReadExistingCoreTableCountsAsync(
        string connectionString)
    {
        var tableNames = new[] { "cross_sections", "bank_points", "calculation_inputs" };
        var counts = new Dictionary<string, long>();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        foreach (var tableName in tableNames)
        {
            using var exists = new NpgsqlCommand("select to_regclass(@table_name) is not null;", connection);
            exists.Parameters.AddWithValue("table_name", "public." + tableName);
            if (!(bool)(await exists.ExecuteScalarAsync() ?? false))
            {
                counts[tableName] = -1;
                continue;
            }

            using var count = new NpgsqlCommand($"select count(*) from public.{tableName};", connection);
            counts[tableName] = Convert.ToInt64(await count.ExecuteScalarAsync());
        }

        return counts;
    }

    private static async Task<int> DeleteTestRowsAsync(string connectionString, string testHash)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand(
            "delete from isodats where source_file_sha256 = @test_hash;", connection);
        command.Parameters.AddWithValue("test_hash", testHash);
        return await command.ExecuteNonQueryAsync();
    }

    private static string CreateIsolatedTestHash(string sourceHash)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sourceHash + "|" + Guid.NewGuid()));
        return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    private static IsodatRecord CopyWithHash(IsodatRecord record, string sourceFileSha256) =>
        new()
        {
            CrossSectionNumber = record.CrossSectionNumber,
            Side = record.Side,
            IsodatType = record.IsodatType,
            Value = record.Value,
            DistanceM = record.DistanceM,
            SourceSheet = record.SourceSheet,
            SourceFileName = record.SourceFileName,
            SourceFileSha256 = sourceFileSha256,
            SourceRow = record.SourceRow,
            ValueSourceColumn = record.ValueSourceColumn,
            DistanceSourceColumn = record.DistanceSourceColumn
        };

    private static string Signature(IsodatRecord record) =>
        $"{record.CrossSectionNumber}|{record.Side}|{record.IsodatType}|{record.Value}|{record.DistanceM}|"
        + $"{record.SourceSheet}|{record.SourceFileName}|{record.SourceFileSha256}|{record.SourceRow}|"
        + $"{record.ValueSourceColumn}|{record.DistanceSourceColumn}";
}

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FLOODZONE_CONN")))
            Skip = "FLOODZONE_CONN не задана; PostgreSQL integration test требует тестовую БД.";
    }
}
