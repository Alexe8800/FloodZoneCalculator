using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class IsodatExcelImportTests
{
    private static readonly string WorkbookPath = Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        "Izodats_Орешко Волга_28082026 11.4.xlsx");

    [Fact]
    public void Workbook_contains_expected_sheets_and_three_cross_sections()
    {
        var workbook = new IsodatExcelWorkbookReader().Read(WorkbookPath);
        var records = ReadRecords();

        Assert.Equal(new[] { "Левый берег", "Правый берег" },
            workbook.Worksheets.Select(sheet => sheet.Name));
        Assert.Equal(new[] { 1, 2, 3 }, records.Select(record => record.CrossSectionNumber).Distinct().OrderBy(number => number));
        Assert.Equal(360, records.Count);
    }

    [Theory]
    [InlineData(IsodatSide.Left, IsodatType.Depth, 24)]
    [InlineData(IsodatSide.Left, IsodatType.Velocity, 18)]
    [InlineData(IsodatSide.Left, IsodatType.CalculatedFloodDuration, 69)]
    [InlineData(IsodatSide.Left, IsodatType.ActualFloodDuration, 69)]
    [InlineData(IsodatSide.Right, IsodatType.Depth, 24)]
    [InlineData(IsodatSide.Right, IsodatType.Velocity, 18)]
    [InlineData(IsodatSide.Right, IsodatType.CalculatedFloodDuration, 69)]
    [InlineData(IsodatSide.Right, IsodatType.ActualFloodDuration, 69)]
    public void Record_counts_match_each_sheet_block(IsodatSide side, IsodatType type, int expectedCount)
    {
        var records = ReadRecords();

        Assert.Equal(expectedCount, records.Count(record => record.Side == side && record.IsodatType == type));
    }

    [Theory]
    [InlineData(IsodatType.Depth, 8)]
    [InlineData(IsodatType.Velocity, 6)]
    [InlineData(IsodatType.CalculatedFloodDuration, 23)]
    [InlineData(IsodatType.ActualFloodDuration, 23)]
    public void Each_section_has_the_observed_count_for_every_type_on_both_sides(
        IsodatType type,
        int expectedCount)
    {
        var records = ReadRecords();

        foreach (var side in Enum.GetValues<IsodatSide>())
        foreach (var sectionNumber in new[] { 1, 2, 3 })
        {
            Assert.Equal(expectedCount, records.Count(record =>
                record.Side == side
                && record.CrossSectionNumber == sectionNumber
                && record.IsodatType == type));
        }
    }

    [Fact]
    public void Left_and_right_sides_and_source_locations_remain_distinct()
    {
        var records = ReadRecords();

        var left = Assert.Single(records, record =>
            record.CrossSectionNumber == 1
            && record.Side == IsodatSide.Left
            && record.IsodatType == IsodatType.Depth
            && record.SourceRow == 5);
        var right = Assert.Single(records, record =>
            record.CrossSectionNumber == 1
            && record.Side == IsodatSide.Right
            && record.IsodatType == IsodatType.Depth
            && record.SourceRow == 5);

        Assert.Equal(6.82m, left.Value);
        Assert.Equal(0m, left.DistanceM);
        Assert.Equal("B", left.ValueSourceColumn);
        Assert.Equal("C", left.DistanceSourceColumn);
        Assert.Equal("Левый берег", left.SourceSheet);
        Assert.Equal(6.82m, right.Value);
        Assert.Equal(0m, right.DistanceM);
        Assert.Equal("Правый берег", right.SourceSheet);
        Assert.NotEqual(left.Side, right.Side);
    }

    [Fact]
    public void Duration_values_and_distances_are_read_from_separate_columns()
    {
        var records = ReadRecords();
        var record = Assert.Single(records, item =>
            item.CrossSectionNumber == 1
            && item.Side == IsodatSide.Left
            && item.IsodatType == IsodatType.CalculatedFloodDuration
            && item.SourceRow == 5);

        Assert.Equal(0.50m, record.Value);
        Assert.Equal(39.35m, record.DistanceM);
        Assert.Equal("J", record.ValueSourceColumn);
        Assert.Equal("K", record.DistanceSourceColumn);
    }

    [Fact]
    public void Reading_the_workbook_is_repeatable_and_does_not_invent_records()
    {
        var first = ReadRecords();
        var second = ReadRecords();

        Assert.Equal(first.Select(Signature), second.Select(Signature));
        Assert.Equal(8, first.Count(record => record.SourceSheet == "Левый берег"
            && record.Side == IsodatSide.Left
            && record.IsodatType == IsodatType.Depth
            && record.CrossSectionNumber == 1));
    }

    private static IReadOnlyList<IsodatRecord> ReadRecords()
    {
        var service = new IsodatExcelImportService(
            new IsodatPostgresRepository("Host=localhost;Database=not-used"));
        return service.ReadRecords(WorkbookPath);
    }

    private static string Signature(IsodatRecord record) =>
        $"{record.CrossSectionNumber}|{record.Side}|{record.IsodatType}|{record.Value}|{record.DistanceM}|"
        + $"{record.SourceSheet}|{record.SourceRow}|{record.ValueSourceColumn}|{record.DistanceSourceColumn}";
}
