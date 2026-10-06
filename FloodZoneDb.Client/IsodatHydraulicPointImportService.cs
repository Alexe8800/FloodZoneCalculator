namespace FloodZoneDb.Client;

public sealed class IsodatHydraulicPointImportResult
{
    public string SourceFileSha256 { get; }
    public int SourceRecordCount { get; }
    public int EligibleRecordCount { get; }
    public int InsertedCount { get; }

    internal IsodatHydraulicPointImportResult(
        string sourceFileSha256,
        int sourceRecordCount,
        int eligibleRecordCount,
        int insertedCount)
    {
        SourceFileSha256 = sourceFileSha256;
        SourceRecordCount = sourceRecordCount;
        EligibleRecordCount = eligibleRecordCount;
        InsertedCount = insertedCount;
    }
}

public sealed class IsodatHydraulicPointImportService
{
    private readonly IsodatPostgresRepository _sourceRepository;
    private readonly IsodatHydraulicPointPostgresRepository _destinationRepository;

    public IsodatHydraulicPointImportService(
        IsodatPostgresRepository sourceRepository,
        IsodatHydraulicPointPostgresRepository destinationRepository)
    {
        _sourceRepository = sourceRepository ?? throw new ArgumentNullException(nameof(sourceRepository));
        _destinationRepository = destinationRepository
            ?? throw new ArgumentNullException(nameof(destinationRepository));
    }

    public async Task<IsodatHydraulicPointImportResult> ImportExistingIsodatsAsync(
        string sourceFileSha256,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFileSha256))
            throw new ArgumentException("SHA-256 исходного файла не задан.", nameof(sourceFileSha256));

        var records = await _sourceRepository.LoadAsync(
            sourceFileSha256: sourceFileSha256,
            cancellationToken: cancellationToken);
        if (records.Count == 0)
            throw new InvalidOperationException(
                "В PostgreSQL не найдены исходные записи isodats для указанного SHA-256.");

        var points = IsodatHydraulicPointMapper.Map(records);
        await _destinationRepository.EnsureSchemaAsync(cancellationToken);
        var inserted = await _destinationRepository.InsertAsync(points, cancellationToken);
        return new IsodatHydraulicPointImportResult(
            sourceFileSha256,
            records.Count,
            points.Count,
            inserted);
    }
}
