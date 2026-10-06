using Npgsql;

namespace FloodZoneDb.Client;

public sealed class IsodatHydraulicPointPostgresRepository
{
    private readonly string _connectionString;

    public IsodatHydraulicPointPostgresRepository(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Строка подключения не задана.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var command = new NpgsqlCommand(ReadSchemaSql(), connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> InsertAsync(
        IReadOnlyCollection<IsodatHydraulicPoint> points,
        CancellationToken cancellationToken = default)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        if (points.Count == 0)
            return 0;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        using var command = new NpgsqlCommand("""
            insert into isodat_hydraulic_points
                (source_isodat_id, cross_section_number, side, x, depth_h, observed_velocity,
                 source_file_sha256, source_sheet, source_row, source_value_column, interpretation)
            values
                (@source_id, @section, @side, @x, @depth, @velocity,
                 @file_hash, @sheet, @row, @value_column, @interpretation)
            on conflict (source_isodat_id) do nothing;
            """, connection, transaction);
        command.Parameters.Add("source_id", NpgsqlTypes.NpgsqlDbType.Bigint);
        command.Parameters.Add("section", NpgsqlTypes.NpgsqlDbType.Integer);
        command.Parameters.Add("side", NpgsqlTypes.NpgsqlDbType.Text);
        command.Parameters.Add("x", NpgsqlTypes.NpgsqlDbType.Numeric);
        command.Parameters.Add("depth", NpgsqlTypes.NpgsqlDbType.Numeric);
        command.Parameters.Add("velocity", NpgsqlTypes.NpgsqlDbType.Numeric);
        command.Parameters.Add("file_hash", NpgsqlTypes.NpgsqlDbType.Char);
        command.Parameters.Add("sheet", NpgsqlTypes.NpgsqlDbType.Text);
        command.Parameters.Add("row", NpgsqlTypes.NpgsqlDbType.Integer);
        command.Parameters.Add("value_column", NpgsqlTypes.NpgsqlDbType.Text);
        command.Parameters.Add("interpretation", NpgsqlTypes.NpgsqlDbType.Text);

        var inserted = 0;
        foreach (var point in points)
        {
            ValidatePoint(point);
            command.Parameters["source_id"].Value = point.SourceIsodatId;
            command.Parameters["section"].Value = point.CrossSectionNumber;
            command.Parameters["side"].Value = point.Side.ToString();
            command.Parameters["x"].Value = point.X;
            command.Parameters["depth"].Value = point.DepthH.HasValue
                ? point.DepthH.Value
                : DBNull.Value;
            command.Parameters["velocity"].Value = point.ObservedVelocity.HasValue
                ? point.ObservedVelocity.Value
                : DBNull.Value;
            command.Parameters["file_hash"].Value = point.SourceFileSha256;
            command.Parameters["sheet"].Value = point.SourceSheet;
            command.Parameters["row"].Value = point.SourceRow;
            command.Parameters["value_column"].Value = point.SourceValueColumn;
            command.Parameters["interpretation"].Value = point.Interpretation;
            inserted += await command.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
        return inserted;
    }

    public async Task<IReadOnlyList<IsodatHydraulicPoint>> LoadAsync(
        string sourceFileSha256,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceFileSha256))
            throw new ArgumentException("SHA-256 исходного файла не задан.", nameof(sourceFileSha256));

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        using var command = new NpgsqlCommand("""
            select id, cross_section_number, side, x, depth_h, observed_velocity, source_isodat_id,
                   source_file_sha256, source_sheet, source_row, source_value_column, interpretation
            from isodat_hydraulic_points
            where source_file_sha256 = @file_hash
            order by source_sheet, source_row, source_value_column, source_isodat_id;
            """, connection);
        command.Parameters.Add("file_hash", NpgsqlTypes.NpgsqlDbType.Char).Value = sourceFileSha256;

        var points = new List<IsodatHydraulicPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!string.Equals(
                    reader.GetString(11),
                    IsodatHydraulicPoint.EducationalSyntheticInterpretation,
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "В таблице найден статус интерпретации, отличный от EducationalSynthetic.");

            points.Add(new IsodatHydraulicPoint(
                reader.GetInt64(0),
                reader.GetInt32(1),
                (IsodatSide)Enum.Parse(typeof(IsodatSide), reader.GetString(2), false),
                reader.GetDecimal(3),
                reader.IsDBNull(4) ? null : reader.GetDecimal(4),
                reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                reader.GetInt64(6),
                reader.GetString(7).Trim(),
                reader.GetString(8),
                reader.GetInt32(9),
                reader.GetString(10)));
        }

        return points;
    }

    private static string ReadSchemaSql()
    {
        using var stream = typeof(IsodatHydraulicPointPostgresRepository).Assembly
            .GetManifestResourceStream("FloodZoneDb.Migrations.CreateIsodatHydraulicPoints.sql")
            ?? throw new InvalidOperationException(
                "В сборку не включена схема таблицы isodat_hydraulic_points.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void ValidatePoint(IsodatHydraulicPoint point)
    {
        if (point == null)
            throw new ArgumentNullException(nameof(point));
        if (point.CrossSectionNumber <= 0
            || point.SourceIsodatId <= 0
            || point.SourceRow <= 0
            || string.IsNullOrWhiteSpace(point.SourceSheet)
            || point.SourceFileSha256.Length != 64
            || !point.SourceFileSha256.All(Uri.IsHexDigit)
            || string.IsNullOrWhiteSpace(point.SourceValueColumn)
            || !Enum.IsDefined(typeof(IsodatSide), point.Side)
            || point.DepthH.HasValue == point.ObservedVelocity.HasValue)
            throw new ArgumentException("Учебная точка изодаты содержит некорректные исходные данные.", nameof(point));
    }
}
