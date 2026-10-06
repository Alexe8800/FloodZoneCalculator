using Npgsql;

namespace FloodZoneDb.Client;

public sealed class IsodatPostgresRepository
{
    private readonly string _connectionString;

    public IsodatPostgresRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Строка подключения не задана.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public static IsodatPostgresRepository FromEnvironment()
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOODZONE_CONN");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Переменная окружения FLOODZONE_CONN не задана.");
        return new IsodatPostgresRepository(connectionString);
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var command = new NpgsqlCommand(ReadSchemaSql(), connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> InsertAsync(
        IReadOnlyCollection<IsodatRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (records == null)
            throw new ArgumentNullException(nameof(records));
        if (records.Count == 0)
            return 0;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        using var command = new NpgsqlCommand("""
            insert into isodats
                (cross_section_number, side, isodat_type, value, distance_m, source_sheet,
                 source_file_name, source_file_sha256, source_row, value_source_column, distance_source_column)
            values
                (@section, @side, @type, @value, @distance, @sheet,
                 @file_name, @file_hash, @row, @value_column, @distance_column)
            on conflict (source_file_sha256, source_sheet, source_row, value_source_column) do nothing;
            """, connection, transaction);
        command.Parameters.Add("section", NpgsqlTypes.NpgsqlDbType.Integer);
        command.Parameters.Add("side", NpgsqlTypes.NpgsqlDbType.Text);
        command.Parameters.Add("type", NpgsqlTypes.NpgsqlDbType.Text);
        command.Parameters.Add("value", NpgsqlTypes.NpgsqlDbType.Numeric);
        command.Parameters.Add("distance", NpgsqlTypes.NpgsqlDbType.Numeric);
        command.Parameters.Add("sheet", NpgsqlTypes.NpgsqlDbType.Text);
        command.Parameters.Add("file_name", NpgsqlTypes.NpgsqlDbType.Text);
        command.Parameters.Add("file_hash", NpgsqlTypes.NpgsqlDbType.Char);
        command.Parameters.Add("row", NpgsqlTypes.NpgsqlDbType.Integer);
        command.Parameters.Add("value_column", NpgsqlTypes.NpgsqlDbType.Text);
        command.Parameters.Add("distance_column", NpgsqlTypes.NpgsqlDbType.Text);

        var inserted = 0;
        foreach (var record in records)
        {
            ValidateRecord(record);
            command.Parameters["section"].Value = record.CrossSectionNumber;
            command.Parameters["side"].Value = record.Side.ToString();
            command.Parameters["type"].Value = record.IsodatType.ToString();
            command.Parameters["value"].Value = record.Value;
            command.Parameters["distance"].Value = record.DistanceM;
            command.Parameters["sheet"].Value = record.SourceSheet;
            command.Parameters["file_name"].Value = record.SourceFileName;
            command.Parameters["file_hash"].Value = record.SourceFileSha256;
            command.Parameters["row"].Value = record.SourceRow;
            command.Parameters["value_column"].Value = record.ValueSourceColumn;
            command.Parameters["distance_column"].Value = record.DistanceSourceColumn;
            inserted += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
        return inserted;
    }

    public async Task<IReadOnlyList<IsodatRecord>> LoadBySourceHashAsync(
        string sourceFileSha256,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFileSha256))
            throw new ArgumentException("SHA-256 исходного файла не задан.", nameof(sourceFileSha256));

        return await LoadAsync(sourceFileSha256: sourceFileSha256, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<IsodatRecord>> LoadAsync(
        string? sourceFileSha256 = null,
        string? sourceFileName = null,
        string? sourceSheet = null,
        int? crossSectionNumber = null,
        IsodatSide? side = null,
        IsodatType? isodatType = null,
        CancellationToken cancellationToken = default)
    {
        if (crossSectionNumber.HasValue && crossSectionNumber.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(crossSectionNumber), "Номер створа должен быть положительным.");
        if (side.HasValue && !Enum.IsDefined(typeof(IsodatSide), side.Value))
            throw new ArgumentOutOfRangeException(nameof(side));
        if (isodatType.HasValue && !Enum.IsDefined(typeof(IsodatType), isodatType.Value))
            throw new ArgumentOutOfRangeException(nameof(isodatType));

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var filters = new List<string>();
        using var command = new NpgsqlCommand("""
            select id, cross_section_number, side, isodat_type, value, distance_m, source_sheet,
                   source_file_name, source_file_sha256, source_row, value_source_column, distance_source_column
            from isodats
            """, connection);
        AddTextFilter(command, filters, "source_file_sha256", "file_hash", sourceFileSha256);
        AddTextFilter(command, filters, "source_file_name", "file_name", sourceFileName);
        AddTextFilter(command, filters, "source_sheet", "sheet", sourceSheet);
        if (crossSectionNumber.HasValue)
        {
            filters.Add("cross_section_number = @section");
            command.Parameters.Add("section", NpgsqlTypes.NpgsqlDbType.Integer).Value = crossSectionNumber.Value;
        }
        if (side.HasValue)
        {
            filters.Add("side = @side");
            command.Parameters.Add("side", NpgsqlTypes.NpgsqlDbType.Text).Value = side.Value.ToString();
        }
        if (isodatType.HasValue)
        {
            filters.Add("isodat_type = @type");
            command.Parameters.Add("type", NpgsqlTypes.NpgsqlDbType.Text).Value = isodatType.Value.ToString();
        }

        if (filters.Count > 0)
            command.CommandText += " where " + string.Join(" and ", filters);
        command.CommandText += " order by source_sheet, source_row, value_source_column;";

        var records = new List<IsodatRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new IsodatRecord
            {
                Id = reader.GetInt64(0),
                CrossSectionNumber = reader.GetInt32(1),
                Side = (IsodatSide)Enum.Parse(typeof(IsodatSide), reader.GetString(2), false),
                IsodatType = (IsodatType)Enum.Parse(typeof(IsodatType), reader.GetString(3), false),
                Value = reader.GetDecimal(4),
                DistanceM = reader.GetDecimal(5),
                SourceSheet = reader.GetString(6),
                SourceFileName = reader.GetString(7),
                SourceFileSha256 = reader.GetString(8).Trim(),
                SourceRow = reader.GetInt32(9),
                ValueSourceColumn = reader.GetString(10),
                DistanceSourceColumn = reader.GetString(11)
            });
        }

        return records;
    }

    private static void AddTextFilter(
        NpgsqlCommand command,
        ICollection<string> filters,
        string columnName,
        string parameterName,
        string? value)
    {
        if (value == null)
            return;
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Значение фильтра не может быть пустым.", parameterName);

        filters.Add($"{columnName} = @{parameterName}");
        command.Parameters.Add(parameterName, NpgsqlTypes.NpgsqlDbType.Text).Value = value;
    }

    private static string ReadSchemaSql()
    {
        using var stream = typeof(IsodatPostgresRepository).Assembly
            .GetManifestResourceStream("FloodZoneDb.Migrations.CreateIsodats.sql")
            ?? throw new InvalidOperationException("В сборку не включена схема таблицы isodats.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void ValidateRecord(IsodatRecord record)
    {
        if (record == null)
            throw new ArgumentNullException(nameof(record));
        if (record.CrossSectionNumber <= 0
            || record.SourceRow <= 0
            || string.IsNullOrWhiteSpace(record.SourceSheet)
            || string.IsNullOrWhiteSpace(record.SourceFileName)
            || record.SourceFileSha256.Length != 64
            || !record.SourceFileSha256.All(Uri.IsHexDigit)
            || string.IsNullOrWhiteSpace(record.ValueSourceColumn)
            || string.IsNullOrWhiteSpace(record.DistanceSourceColumn)
            || !Enum.IsDefined(typeof(IsodatSide), record.Side)
            || !Enum.IsDefined(typeof(IsodatType), record.IsodatType))
            throw new ArgumentException("Запись изодаты содержит некорректные исходные данные.", nameof(record));
    }
}
