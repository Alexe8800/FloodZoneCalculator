namespace FloodZoneDb.Client;

public static class IsodatHydraulicPointMapper
{
    public static IReadOnlyList<IsodatHydraulicPoint> Map(
        IReadOnlyList<IsodatRecord> records)
    {
        if (records == null)
            throw new ArgumentNullException(nameof(records));

        var points = new List<IsodatHydraulicPoint>();
        foreach (var record in records)
        {
            if (record == null)
                throw new ArgumentException("Коллекция содержит пустую запись изодаты.", nameof(records));

            if (record.IsodatType is not (IsodatType.Depth or IsodatType.Velocity))
                continue;

            if (record.Id <= 0)
                throw new ArgumentException(
                    "Для преобразования нужна сохранённая в PostgreSQL запись изодаты с положительным Id.",
                    nameof(records));

            points.Add(new IsodatHydraulicPoint(
                0,
                record.CrossSectionNumber,
                record.Side,
                record.DistanceM,
                record.IsodatType == IsodatType.Depth ? record.Value : null,
                record.IsodatType == IsodatType.Velocity ? record.Value : null,
                record.Id,
                record.SourceFileSha256,
                record.SourceSheet,
                record.SourceRow,
                record.ValueSourceColumn));
        }

        return Array.AsReadOnly(points.ToArray());
    }
}
