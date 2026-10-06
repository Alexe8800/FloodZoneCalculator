using System.Globalization;

namespace FloodZoneDb.Client;

public sealed class IsodatExcelRecordMapper
{
    private static readonly IReadOnlyList<BlockLayout> LeftLayouts = new[]
    {
        new BlockLayout(IsodatType.Depth, 1, 2, 3, "hi_l", "Ri_l"),
        new BlockLayout(IsodatType.Velocity, 5, 6, 7, "vi_l", "Ri_l"),
        new BlockLayout(IsodatType.CalculatedFloodDuration, 9, 10, 11, "t_osi_ras", "Ri_l"),
        new BlockLayout(IsodatType.ActualFloodDuration, 13, 14, 15, "t_osi_fakt", "Ri_l")
    };

    private static readonly IReadOnlyList<BlockLayout> RightLayouts = new[]
    {
        new BlockLayout(IsodatType.Depth, 1, 2, 3, "hi_p", "Ri_p"),
        new BlockLayout(IsodatType.Velocity, 5, 6, 7, "vi_p", "Ri_p"),
        new BlockLayout(IsodatType.CalculatedFloodDuration, 9, 10, 11, "Tos_p", "Ri_p"),
        new BlockLayout(IsodatType.ActualFloodDuration, 13, 14, 15, "Tfz_p", "Ri_p")
    };

    public IReadOnlyList<IsodatRecord> Map(
        IsodatExcelWorkbook workbook,
        string sourceFileName,
        string sourceFileSha256)
    {
        if (workbook == null)
            throw new ArgumentNullException(nameof(workbook));
        if (string.IsNullOrWhiteSpace(sourceFileName))
            throw new ArgumentException("Имя исходного файла не задано.", nameof(sourceFileName));
        if (string.IsNullOrWhiteSpace(sourceFileSha256))
            throw new ArgumentException("SHA-256 исходного файла не задан.", nameof(sourceFileSha256));

        var records = new List<IsodatRecord>();
        MapSide(workbook, "Левый берег", IsodatSide.Left, LeftLayouts, sourceFileName, sourceFileSha256, records);
        MapSide(workbook, "Правый берег", IsodatSide.Right, RightLayouts, sourceFileName, sourceFileSha256, records);
        if (records.Count == 0)
            throw new InvalidDataException("В книге не найдено записей изодат.");
        return records;
    }

    private static void MapSide(
        IsodatExcelWorkbook workbook,
        string sheetName,
        IsodatSide side,
        IReadOnlyList<BlockLayout> layouts,
        string sourceFileName,
        string sourceFileSha256,
        ICollection<IsodatRecord> records)
    {
        var sheet = workbook.Worksheets.SingleOrDefault(item =>
            string.Equals(item.Name, sheetName, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"В книге не найден обязательный лист «{sheetName}».");

        foreach (var layout in layouts)
        {
            ValidateHeaders(sheet, layout);
            foreach (var rowNumber in sheet.Rows.Keys.Where(row => row >= 5).OrderBy(row => row))
            {
                var sectionValue = sheet.GetCell(rowNumber, layout.SectionColumn);
                var isodatValue = sheet.GetCell(rowNumber, layout.ValueColumn);
                var distanceValue = sheet.GetCell(rowNumber, layout.DistanceColumn);
                if (sectionValue.Length == 0 && isodatValue.Length == 0 && distanceValue.Length == 0)
                    continue;

                if (sectionValue.Length == 0 || isodatValue.Length == 0 || distanceValue.Length == 0)
                    throw new InvalidDataException(
                        $"Неполная запись {layout.Type} на листе «{sheetName}», строка {rowNumber}.");

                if (!int.TryParse(sectionValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sectionNumber)
                    || sectionNumber <= 0)
                    throw new InvalidDataException(
                        $"Некорректный номер створа «{sectionValue}» на листе «{sheetName}», строка {rowNumber}.");

                records.Add(new IsodatRecord
                {
                    CrossSectionNumber = sectionNumber,
                    Side = side,
                    IsodatType = layout.Type,
                    Value = ParseDecimal(isodatValue, sheetName, rowNumber, layout.ValueColumn),
                    DistanceM = ParseDecimal(distanceValue, sheetName, rowNumber, layout.DistanceColumn),
                    SourceSheet = sheetName,
                    SourceFileName = sourceFileName,
                    SourceFileSha256 = sourceFileSha256,
                    SourceRow = rowNumber,
                    ValueSourceColumn = ColumnName(layout.ValueColumn),
                    DistanceSourceColumn = ColumnName(layout.DistanceColumn)
                });
            }
        }
    }

    private static void ValidateHeaders(IsodatExcelWorksheet sheet, BlockLayout layout)
    {
        var actualSectionHeader = sheet.GetCell(3, layout.SectionColumn);
        var actualValueHeader = sheet.GetCell(3, layout.ValueColumn);
        var actualDistanceHeader = sheet.GetCell(3, layout.DistanceColumn);
        if (actualSectionHeader != "N_stvora"
            || actualValueHeader != layout.ExpectedValueHeader
            || actualDistanceHeader != layout.ExpectedDistanceHeader)
            throw new InvalidDataException(
                $"Структура блока {layout.Type} на листе «{sheet.Name}» не совпадает с ожидаемым заголовком.");
    }

    private static decimal ParseDecimal(string value, string sheetName, int rowNumber, int columnNumber)
    {
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue))
            return invariantValue;
        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("ru-RU"), out var russianValue))
            return russianValue;

        throw new InvalidDataException(
            $"Некорректное число «{value}» на листе «{sheetName}», ячейка {ColumnName(columnNumber)}{rowNumber}.");
    }

    private static string ColumnName(int columnNumber)
    {
        var result = "";
        while (columnNumber > 0)
        {
            columnNumber--;
            result = (char)('A' + columnNumber % 26) + result;
            columnNumber /= 26;
        }

        return result;
    }

    private sealed class BlockLayout
    {
        public IsodatType Type { get; }
        public int SectionColumn { get; }
        public int ValueColumn { get; }
        public int DistanceColumn { get; }
        public string ExpectedValueHeader { get; }
        public string ExpectedDistanceHeader { get; }

        public BlockLayout(
            IsodatType type,
            int sectionColumn,
            int valueColumn,
            int distanceColumn,
            string expectedValueHeader,
            string expectedDistanceHeader)
        {
            Type = type;
            SectionColumn = sectionColumn;
            ValueColumn = valueColumn;
            DistanceColumn = distanceColumn;
            ExpectedValueHeader = expectedValueHeader;
            ExpectedDistanceHeader = expectedDistanceHeader;
        }
    }
}
