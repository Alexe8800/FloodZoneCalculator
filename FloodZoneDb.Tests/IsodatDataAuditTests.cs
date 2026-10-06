using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class IsodatDataAuditTests
{
    [Fact]
    public void Audit_reports_total_and_type_counts()
    {
        var records = CreateRecords();
        var audit = Audit(records);

        Assert.Equal(records.Length, audit.TotalCount);
        Assert.Equal(
            new[]
            {
                (IsodatType.Depth, 4),
                (IsodatType.Velocity, 2),
                (IsodatType.CalculatedFloodDuration, 2),
                (IsodatType.ActualFloodDuration, 2)
            },
            audit.TypeTotals.Select(total => (total.IsodatType, total.Count)));
        Assert.Equal(3, audit.Rows.Where(row => row.CrossSectionNumber == 1).Sum(row => row.Count));
        Assert.Equal(5, audit.Rows.Where(row => row.Side == IsodatSide.Left).Sum(row => row.Count));
        Assert.Equal(3, audit.Rows.Count(row => row.CrossSectionNumber == 1));
    }

    [Fact]
    public void Audit_reports_numeric_ranges_and_distinct_counts_without_transforming_values()
    {
        var records = new[]
        {
            CreateRecord(1, IsodatSide.Left, IsodatType.Depth, -1.25m, 4.5m),
            CreateRecord(1, IsodatSide.Left, IsodatType.Depth, 7.75m, 2m),
            CreateRecord(1, IsodatSide.Left, IsodatType.Depth, 7.75m, 4.5m)
        };

        var row = Assert.Single(Audit(records).Rows);

        Assert.Equal(3, row.Count);
        Assert.Equal(-1.25m, row.MinValue);
        Assert.Equal(7.75m, row.MaxValue);
        Assert.Equal(2m, row.MinDistanceM);
        Assert.Equal(4.5m, row.MaxDistanceM);
        Assert.Equal(2, row.UniqueValues);
        Assert.Equal(2, row.UniqueDistances);
    }

    [Fact]
    public void Audit_omits_absent_section_side_type_rows_and_reports_zero_type_totals()
    {
        var records = new[]
        {
            CreateRecord(2, IsodatSide.Right, IsodatType.Velocity, 5m, 8m)
        };

        var audit = Audit(records);
        var row = Assert.Single(audit.Rows);
        var absentTotal = Assert.Single(
            audit.TypeTotals,
            total => total.IsodatType == IsodatType.Depth);

        Assert.Equal(2, row.CrossSectionNumber);
        Assert.Equal(IsodatSide.Right, row.Side);
        Assert.Equal(IsodatType.Velocity, row.IsodatType);
        Assert.Equal(1, row.Count);
        Assert.Equal(0, absentTotal.Count);
        Assert.Null(absentTotal.MinValue);
        Assert.Null(absentTotal.MaxValue);
        Assert.Null(absentTotal.MinDistanceM);
        Assert.Null(absentTotal.MaxDistanceM);
        Assert.Equal(0, absentTotal.UniqueValues);
        Assert.Equal(0, absentTotal.UniqueDistances);
    }

    [Fact]
    public void Audit_of_empty_grouping_returns_empty_rows_and_zero_totals()
    {
        var audit = IsodatDataAuditor.Audit(
            IsodatGrouper.GroupByCrossSection(Array.Empty<IsodatRecord>()));

        Assert.Equal(0, audit.TotalCount);
        Assert.Empty(audit.Rows);
        Assert.All(audit.TypeTotals, total =>
        {
            Assert.Equal(0, total.Count);
            Assert.Null(total.MinValue);
            Assert.Null(total.MaxValue);
            Assert.Null(total.MinDistanceM);
            Assert.Null(total.MaxDistanceM);
            Assert.Equal(0, total.UniqueValues);
            Assert.Equal(0, total.UniqueDistances);
        });
    }

    [Fact]
    public void Audit_does_not_mutate_input_and_is_repeatable()
    {
        var records = CreateRecords();
        var before = records.Select(Signature).ToArray();
        var grouping = IsodatGrouper.GroupByCrossSection(records);

        var first = IsodatDataAuditor.Audit(grouping);
        var second = IsodatDataAuditor.Audit(grouping);

        Assert.Equal(before, records.Select(Signature));
        Assert.Equal(AuditSignature(first), AuditSignature(second));
    }

    private static IsodatRecord[] CreateRecords() =>
        new[]
        {
            CreateRecord(1, IsodatSide.Left, IsodatType.Depth, 1m, 10m),
            CreateRecord(1, IsodatSide.Left, IsodatType.Velocity, 2m, 20m),
            CreateRecord(1, IsodatSide.Right, IsodatType.Depth, 3m, 30m),
            CreateRecord(2, IsodatSide.Left, IsodatType.Depth, 4m, 40m),
            CreateRecord(2, IsodatSide.Right, IsodatType.Velocity, 5m, 50m),
            CreateRecord(3, IsodatSide.Left, IsodatType.CalculatedFloodDuration, 6m, 60m),
            CreateRecord(3, IsodatSide.Right, IsodatType.ActualFloodDuration, 7m, 70m),
            CreateRecord(3, IsodatSide.Right, IsodatType.Depth, 8m, 80m),
            CreateRecord(3, IsodatSide.Left, IsodatType.CalculatedFloodDuration, 6m, 60m),
            CreateRecord(3, IsodatSide.Right, IsodatType.ActualFloodDuration, 9m, 90m)
        };

    private static IsodatDataAudit Audit(IReadOnlyList<IsodatRecord> records) =>
        IsodatDataAuditor.Audit(IsodatGrouper.GroupByCrossSection(records));

    private static IsodatRecord CreateRecord(
        int section,
        IsodatSide side,
        IsodatType type,
        decimal value,
        decimal distance) =>
        new()
        {
            CrossSectionNumber = section,
            Side = side,
            IsodatType = type,
            Value = value,
            DistanceM = distance
        };

    private static string Signature(IsodatRecord record) =>
        $"{record.CrossSectionNumber}|{record.Side}|{record.IsodatType}|{record.Value}|{record.DistanceM}";

    private static string AuditSignature(IsodatDataAudit audit) =>
        $"{audit.TotalCount}\n"
        + string.Join("\n", audit.Rows.Select(row =>
            $"{row.CrossSectionNumber}|{row.Side}|{row.IsodatType}|{row.Count}|{row.MinValue}|{row.MaxValue}|"
            + $"{row.MinDistanceM}|{row.MaxDistanceM}|{row.UniqueValues}|{row.UniqueDistances}"))
        + "\n"
        + string.Join("\n", audit.TypeTotals.Select(total =>
            $"{total.IsodatType}|{total.Count}|{total.MinValue}|{total.MaxValue}|{total.MinDistanceM}|"
            + $"{total.MaxDistanceM}|{total.UniqueValues}|{total.UniqueDistances}"));
}
