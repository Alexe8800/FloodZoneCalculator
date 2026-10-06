namespace FloodZoneDb.Client;

public sealed class IsodatDataAudit
{
    public int TotalCount { get; }
    public IReadOnlyList<IsodatDataAuditRow> Rows { get; }
    public IReadOnlyList<IsodatTypeAuditTotal> TypeTotals { get; }

    internal IsodatDataAudit(
        int totalCount,
        IEnumerable<IsodatDataAuditRow> rows,
        IEnumerable<IsodatTypeAuditTotal> typeTotals)
    {
        TotalCount = totalCount;
        Rows = Array.AsReadOnly(rows.ToArray());
        TypeTotals = Array.AsReadOnly(typeTotals.ToArray());
    }
}

public sealed class IsodatDataAuditRow
{
    public int CrossSectionNumber { get; }
    public IsodatSide Side { get; }
    public IsodatType IsodatType { get; }
    public int Count { get; }
    public decimal? MinValue { get; }
    public decimal? MaxValue { get; }
    public decimal? MinDistanceM { get; }
    public decimal? MaxDistanceM { get; }
    public int UniqueValues { get; }
    public int UniqueDistances { get; }

    internal IsodatDataAuditRow(
        int crossSectionNumber,
        IsodatSide side,
        IsodatType isodatType,
        IReadOnlyList<IsodatRecord> records)
    {
        CrossSectionNumber = crossSectionNumber;
        Side = side;
        IsodatType = isodatType;
        Count = records.Count;
        MinValue = records.Count == 0 ? null : records.Min(record => record.Value);
        MaxValue = records.Count == 0 ? null : records.Max(record => record.Value);
        MinDistanceM = records.Count == 0 ? null : records.Min(record => record.DistanceM);
        MaxDistanceM = records.Count == 0 ? null : records.Max(record => record.DistanceM);
        UniqueValues = records.Select(record => record.Value).Distinct().Count();
        UniqueDistances = records.Select(record => record.DistanceM).Distinct().Count();
    }
}

public sealed class IsodatTypeAuditTotal
{
    public IsodatType IsodatType { get; }
    public int Count { get; }
    public decimal? MinValue { get; }
    public decimal? MaxValue { get; }
    public decimal? MinDistanceM { get; }
    public decimal? MaxDistanceM { get; }
    public int UniqueValues { get; }
    public int UniqueDistances { get; }

    internal IsodatTypeAuditTotal(IsodatType isodatType, IReadOnlyList<IsodatRecord> records)
    {
        IsodatType = isodatType;
        Count = records.Count;
        MinValue = records.Count == 0 ? null : records.Min(record => record.Value);
        MaxValue = records.Count == 0 ? null : records.Max(record => record.Value);
        MinDistanceM = records.Count == 0 ? null : records.Min(record => record.DistanceM);
        MaxDistanceM = records.Count == 0 ? null : records.Max(record => record.DistanceM);
        UniqueValues = records.Select(record => record.Value).Distinct().Count();
        UniqueDistances = records.Select(record => record.DistanceM).Distinct().Count();
    }
}

public static class IsodatDataAuditor
{
    public static IsodatDataAudit Audit(IsodatGrouping grouping)
    {
        if (grouping == null)
            throw new ArgumentNullException(nameof(grouping));

        var rows = new List<IsodatDataAuditRow>();
        var types = Enum.GetValues(typeof(IsodatType)).Cast<IsodatType>().ToArray();
        var recordsByType = types
            .ToDictionary(type => type, _ => new List<IsodatRecord>());

        foreach (var section in grouping.CrossSections)
        {
            foreach (var side in new[] { section.Left, section.Right })
            {
                if (side == null)
                    continue;

                foreach (var pair in side.ByType.OrderBy(pair => pair.Key))
                {
                    rows.Add(new IsodatDataAuditRow(
                        section.CrossSectionNumber,
                        side.Side,
                        pair.Key,
                        pair.Value));
                    recordsByType[pair.Key].AddRange(pair.Value);
                }
            }
        }

        var totals = types
            .Select(type => new IsodatTypeAuditTotal(type, recordsByType[type]));
        return new IsodatDataAudit(
            rows.Sum(row => row.Count),
            rows,
            totals);
    }
}
