using FloodZoneDb.Client;
using Xunit;

namespace FloodZoneDb.Tests;

public sealed class IsodatGrouperTests
{
    [Fact]
    public void Groups_three_sections_by_side_and_all_isodat_types()
    {
        var input = CreateCompleteRecords();
        var result = IsodatGrouper.GroupByCrossSection(input);

        Assert.Equal(new[] { 1, 2, 3 }, result.CrossSections.Select(section => section.CrossSectionNumber));
        Assert.Equal(3, result.CrossSections.Count);
        foreach (var section in result.CrossSections)
        {
            Assert.NotNull(section.Left);
            Assert.NotNull(section.Right);
            Assert.Equal(IsodatSide.Left, section.Left.Side);
            Assert.Equal(IsodatSide.Right, section.Right.Side);
            Assert.Equal(Enum.GetValues<IsodatType>(), section.Left.ByType.Keys.OrderBy(type => type));
            Assert.Equal(Enum.GetValues<IsodatType>(), section.Right.ByType.Keys.OrderBy(type => type));
        }
    }

    [Fact]
    public void Preserves_value_distance_and_record_metadata_without_reinterpretation()
    {
        var source = CreateRecord(2, IsodatSide.Right, IsodatType.Depth, 4.25m, 19.75m);
        var result = IsodatGrouper.GroupByCrossSection(new[] { source });
        var grouped = Assert.Single(Assert.Single(result.CrossSections).Right!.ByType[IsodatType.Depth]);

        Assert.Same(source, grouped);
        Assert.Equal(source.Value, grouped.Value);
        Assert.Equal(source.DistanceM, grouped.DistanceM);
        Assert.Equal(source.Side, grouped.Side);
        Assert.Equal(source.IsodatType, grouped.IsodatType);
        Assert.Equal(source.CrossSectionNumber, grouped.CrossSectionNumber);
        Assert.Equal(source.SourceSheet, grouped.SourceSheet);
        Assert.Equal(source.SourceFileName, grouped.SourceFileName);
        Assert.Equal(source.SourceFileSha256, grouped.SourceFileSha256);
        Assert.Equal(source.SourceRow, grouped.SourceRow);
        Assert.Equal(source.ValueSourceColumn, grouped.ValueSourceColumn);
        Assert.Equal(source.DistanceSourceColumn, grouped.DistanceSourceColumn);
    }

    [Fact]
    public void Keeps_missing_side_and_type_absent()
    {
        var leftDepth = CreateRecord(1, IsodatSide.Left, IsodatType.Depth, 2m, 4m);
        var result = IsodatGrouper.GroupByCrossSection(new[] { leftDepth });
        var section = Assert.Single(result.CrossSections);

        Assert.NotNull(section.Left);
        Assert.Null(section.Right);
        Assert.Equal(new[] { IsodatType.Depth }, section.Left.ByType.Keys);
        Assert.False(section.Left.ByType.ContainsKey(IsodatType.Velocity));
        Assert.Empty(IsodatGrouper.GroupByCrossSection(Array.Empty<IsodatRecord>()).CrossSections);
    }

    [Fact]
    public void Preserves_asymmetric_record_counts_without_padding()
    {
        var input = new[]
        {
            CreateRecord(1, IsodatSide.Left, IsodatType.Depth, 1m, 1m),
            CreateRecord(1, IsodatSide.Left, IsodatType.Depth, 2m, 2m),
            CreateRecord(1, IsodatSide.Right, IsodatType.Velocity, 3m, 3m),
            CreateRecord(2, IsodatSide.Right, IsodatType.ActualFloodDuration, 4m, 4m)
        };

        var result = IsodatGrouper.GroupByCrossSection(input);
        var first = result.CrossSections[0];
        var second = result.CrossSections[1];

        Assert.Equal(2, first.Left!.ByType[IsodatType.Depth].Count);
        Assert.Null(first.Right!.ByType.GetValueOrDefault(IsodatType.Depth));
        Assert.Single(first.Right.ByType[IsodatType.Velocity]);
        Assert.Null(second.Left);
        Assert.Single(second.Right!.ByType[IsodatType.ActualFloodDuration]);
        Assert.Equal(4, result.CrossSections.Sum(section =>
            (section.Left?.ByType.Values.Sum(values => values.Count) ?? 0)
            + (section.Right?.ByType.Values.Sum(values => values.Count) ?? 0)));
    }

    [Fact]
    public void Does_not_mutate_input_and_preserves_order_within_each_type()
    {
        var input = new[]
        {
            CreateRecord(3, IsodatSide.Left, IsodatType.Depth, 3m, 3m),
            CreateRecord(1, IsodatSide.Left, IsodatType.Depth, 1m, 1m),
            CreateRecord(3, IsodatSide.Left, IsodatType.Depth, 4m, 4m),
            CreateRecord(1, IsodatSide.Right, IsodatType.Velocity, 2m, 2m)
        };
        var before = input.Select(Signature).ToArray();

        var result = IsodatGrouper.GroupByCrossSection(input);

        Assert.Equal(before, input.Select(Signature));
        Assert.Equal(new[] { 1, 3 }, result.CrossSections.Select(section => section.CrossSectionNumber));
        Assert.Equal(new[] { 3m, 4m }, result.CrossSections[1].Left!.ByType[IsodatType.Depth].Select(record => record.Value));
        Assert.Equal(4, input.Length);
    }

    [Fact]
    public void Repeated_grouping_returns_the_same_group_structure_and_records()
    {
        var input = CreateCompleteRecords();

        var first = IsodatGrouper.GroupByCrossSection(input);
        var second = IsodatGrouper.GroupByCrossSection(input);

        Assert.Equal(GroupingSignature(first), GroupingSignature(second));
        Assert.Equal(input.Length, first.CrossSections.Sum(section =>
            (section.Left?.ByType.Values.Sum(values => values.Count) ?? 0)
            + (section.Right?.ByType.Values.Sum(values => values.Count) ?? 0)));
    }

    [Fact]
    public void GetSet_returns_only_the_requested_cross_section_and_side()
    {
        var input = CreateCompleteRecords();
        var grouping = IsodatGrouper.GroupByCrossSection(input);

        foreach (var sectionNumber in new[] { 1, 2, 3 })
        foreach (var side in Enum.GetValues<IsodatSide>())
        {
            var set = Assert.IsType<IsodatSideIsodats>(grouping.GetSet(sectionNumber, side));
            var expected = input.Where(record =>
                record.CrossSectionNumber == sectionNumber && record.Side == side);

            Assert.Equal(side, set.Side);
            Assert.Equal(
                expected.Select(Signature),
                set.ByType.Values.SelectMany(records => records).Select(Signature));
            Assert.All(
                set.ByType.Values.SelectMany(records => records),
                record => Assert.Equal(sectionNumber, record.CrossSectionNumber));
        }
    }

    [Fact]
    public void GetSet_keeps_absent_section_side_and_type_absent()
    {
        var input = new[]
        {
            CreateRecord(1, IsodatSide.Left, IsodatType.Depth, 2m, 4m),
            CreateRecord(2, IsodatSide.Right, IsodatType.Velocity, 3m, 5m)
        };
        var grouping = IsodatGrouper.GroupByCrossSection(input);

        var firstLeft = Assert.IsType<IsodatSideIsodats>(
            grouping.GetSet(1, IsodatSide.Left));
        Assert.Null(grouping.GetSet(1, IsodatSide.Right));
        Assert.Null(grouping.GetSet(2, IsodatSide.Left));
        Assert.Null(grouping.GetSet(3, IsodatSide.Left));
        Assert.Equal(new[] { IsodatType.Depth }, firstLeft.ByType.Keys);
        Assert.False(firstLeft.ByType.ContainsKey(IsodatType.Velocity));
    }

    [Fact]
    public void GetSet_preserves_values_distances_record_order_and_input()
    {
        var input = new[]
        {
            CreateRecord(1, IsodatSide.Left, IsodatType.Depth, 8.25m, 21.5m),
            CreateRecord(2, IsodatSide.Left, IsodatType.Depth, 99m, 99m),
            CreateRecord(1, IsodatSide.Left, IsodatType.Depth, 7.5m, 18m),
            CreateRecord(1, IsodatSide.Right, IsodatType.Depth, 100m, 100m)
        };
        var inputBefore = input.Select(Signature).ToArray();
        var grouping = IsodatGrouper.GroupByCrossSection(input);

        var firstRead = Assert.IsType<IsodatSideIsodats>(
            grouping.GetSet(1, IsodatSide.Left));
        var secondRead = Assert.IsType<IsodatSideIsodats>(
            grouping.GetSet(1, IsodatSide.Left));
        var selected = firstRead.ByType[IsodatType.Depth];

        Assert.Equal(new[] { 8.25m, 7.5m }, selected.Select(record => record.Value));
        Assert.Equal(new[] { 21.5m, 18m }, selected.Select(record => record.DistanceM));
        Assert.Equal(new[] { 1, 1 }, selected.Select(record => record.CrossSectionNumber));
        Assert.All(selected, record => Assert.Equal(IsodatSide.Left, record.Side));
        Assert.Equal(selected.Select(Signature), secondRead.ByType[IsodatType.Depth].Select(Signature));
        Assert.Equal(inputBefore, input.Select(Signature));
        Assert.Equal(4, input.Length);
    }

    [Fact]
    public void GetSet_keeps_left_and_right_independent_for_asymmetric_data()
    {
        var input = new[]
        {
            CreateRecord(1, IsodatSide.Left, IsodatType.Depth, 1m, 11m),
            CreateRecord(1, IsodatSide.Left, IsodatType.Depth, 2m, 12m),
            CreateRecord(1, IsodatSide.Right, IsodatType.Velocity, 3m, 13m),
            CreateRecord(3, IsodatSide.Right, IsodatType.ActualFloodDuration, 4m, 14m)
        };
        var grouping = IsodatGrouper.GroupByCrossSection(input);

        var firstLeft = Assert.IsType<IsodatSideIsodats>(
            grouping.GetSet(1, IsodatSide.Left));
        var firstRight = Assert.IsType<IsodatSideIsodats>(
            grouping.GetSet(1, IsodatSide.Right));
        var thirdRight = Assert.IsType<IsodatSideIsodats>(
            grouping.GetSet(3, IsodatSide.Right));

        Assert.Equal(new[] { IsodatType.Depth }, firstLeft.ByType.Keys);
        Assert.Equal(2, firstLeft.ByType[IsodatType.Depth].Count);
        Assert.Equal(new[] { IsodatType.Velocity }, firstRight.ByType.Keys);
        Assert.Single(firstRight.ByType[IsodatType.Velocity]);
        Assert.Equal(new[] { IsodatType.ActualFloodDuration }, thirdRight.ByType.Keys);
        Assert.Single(thirdRight.ByType[IsodatType.ActualFloodDuration]);
        Assert.Null(grouping.GetSet(3, IsodatSide.Left));
    }

    private static IsodatRecord[] CreateCompleteRecords() =>
        (from section in new[] { 1, 2, 3 }
         from side in Enum.GetValues<IsodatSide>()
         from type in Enum.GetValues<IsodatType>()
         select CreateRecord(section, side, type, section * 10m + (int)type, section * 100m + (int)type))
        .ToArray();

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
            DistanceM = distance,
            SourceSheet = side == IsodatSide.Left ? "Левый берег" : "Правый берег",
            SourceFileName = "source.xlsx",
            SourceFileSha256 = new string('a', 64),
            SourceRow = 10 + section,
            ValueSourceColumn = "B",
            DistanceSourceColumn = "C"
        };

    private static string Signature(IsodatRecord record) =>
        $"{record.CrossSectionNumber}|{record.Side}|{record.IsodatType}|{record.Value}|{record.DistanceM}|"
        + $"{record.SourceSheet}|{record.SourceFileName}|{record.SourceFileSha256}|{record.SourceRow}|"
        + $"{record.ValueSourceColumn}|{record.DistanceSourceColumn}";

    private static string GroupingSignature(IsodatGrouping grouping) =>
        string.Join("\n", grouping.CrossSections.SelectMany(section =>
            new[] { section.Left, section.Right }
                .Where(side => side != null)
                .SelectMany(side => side!.ByType.SelectMany(type =>
                    type.Value.Select(record => Signature(record))))));
}
