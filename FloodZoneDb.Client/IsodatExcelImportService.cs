using System.Security.Cryptography;
using System.IO;

namespace FloodZoneDb.Client;

public sealed class IsodatExcelImportResult
{
    public string SourceFileSha256 { get; }
    public int RecordCount { get; }
    public int InsertedCount { get; }

    internal IsodatExcelImportResult(string sourceFileSha256, int recordCount, int insertedCount)
    {
        SourceFileSha256 = sourceFileSha256;
        RecordCount = recordCount;
        InsertedCount = insertedCount;
    }
}

public sealed class IsodatExcelImportService
{
    private readonly IsodatExcelWorkbookReader _workbookReader;
    private readonly IsodatExcelRecordMapper _recordMapper;
    private readonly IsodatPostgresRepository _repository;

    public IsodatExcelImportService(IsodatPostgresRepository repository)
        : this(repository, new IsodatExcelWorkbookReader(), new IsodatExcelRecordMapper())
    {
    }

    public IsodatExcelImportService(
        IsodatPostgresRepository repository,
        IsodatExcelWorkbookReader workbookReader,
        IsodatExcelRecordMapper recordMapper)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _workbookReader = workbookReader ?? throw new ArgumentNullException(nameof(workbookReader));
        _recordMapper = recordMapper ?? throw new ArgumentNullException(nameof(recordMapper));
    }

    public IReadOnlyList<IsodatRecord> ReadRecords(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Путь к Excel-файлу не задан.", nameof(filePath));

        var sourceBytes = File.ReadAllBytes(filePath);
        using var sha256 = SHA256.Create();
        var sourceHash = BitConverter.ToString(sha256.ComputeHash(sourceBytes)).Replace("-", "").ToLowerInvariant();
        using var stream = new MemoryStream(sourceBytes);
        var workbook = _workbookReader.Read(stream);
        return _recordMapper.Map(workbook, Path.GetFileName(filePath), sourceHash);
    }

    public async Task<IsodatExcelImportResult> ImportAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var records = ReadRecords(filePath);
        await _repository.EnsureSchemaAsync(cancellationToken);
        var insertedCount = await _repository.InsertAsync(records, cancellationToken);
        return new IsodatExcelImportResult(records[0].SourceFileSha256, records.Count, insertedCount);
    }
}
