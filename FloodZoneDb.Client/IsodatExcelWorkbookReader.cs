using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace FloodZoneDb.Client;

public sealed class IsodatExcelWorkbook
{
    public IReadOnlyList<IsodatExcelWorksheet> Worksheets { get; }

    internal IsodatExcelWorkbook(IReadOnlyList<IsodatExcelWorksheet> worksheets)
    {
        Worksheets = worksheets;
    }
}

public sealed class IsodatExcelWorksheet
{
    public string Name { get; }
    public IReadOnlyDictionary<int, IReadOnlyDictionary<int, string>> Rows { get; }

    internal IsodatExcelWorksheet(
        string name,
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, string>> rows)
    {
        Name = name;
        Rows = rows;
    }

    public string GetCell(int rowNumber, int columnNumber) =>
        Rows.TryGetValue(rowNumber, out var row) && row.TryGetValue(columnNumber, out var value)
            ? value
            : "";
}

public sealed class IsodatExcelWorkbookReader
{
    private static readonly XNamespace MainNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNamespace =
        "http://schemas.openxmlformats.org/package/2006/relationships";

    public IsodatExcelWorkbook Read(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Путь к Excel-файлу не задан.", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Excel-файл не найден.", filePath);

        using var stream = File.OpenRead(filePath);
        return Read(stream);
    }

    public IsodatExcelWorkbook Read(Stream stream)
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead)
            throw new ArgumentException("Поток Excel-файла недоступен для чтения.", nameof(stream));

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        var relationshipTargets = relationships
            .Root!
            .Elements(PackageRelationshipNamespace + "Relationship")
            .ToDictionary(
                element => (string)element.Attribute("Id")!,
                element => NormalizeWorksheetPath((string)element.Attribute("Target")!));
        var sharedStrings = ReadSharedStrings(archive);
        var worksheets = new List<IsodatExcelWorksheet>();

        foreach (var sheet in workbook.Descendants(MainNamespace + "sheet"))
        {
            var name = (string?)sheet.Attribute("name")
                ?? throw new InvalidDataException("В книге найден лист без имени.");
            var relationshipId = (string?)sheet.Attribute(RelationshipNamespace + "id")
                ?? throw new InvalidDataException($"У листа «{name}» отсутствует ссылка на worksheet.");
            if (!relationshipTargets.TryGetValue(relationshipId, out var worksheetPath))
                throw new InvalidDataException($"Не найдена связь для листа «{name}».");

            var worksheetXml = LoadXml(archive, worksheetPath);
            var rows = new Dictionary<int, IReadOnlyDictionary<int, string>>();
            foreach (var row in worksheetXml.Descendants(MainNamespace + "sheetData")
                         .Elements(MainNamespace + "row"))
            {
                var rowNumber = ParsePositiveInteger((string?)row.Attribute("r"), "номер строки");
                var cells = new Dictionary<int, string>();
                foreach (var cell in row.Elements(MainNamespace + "c"))
                {
                    var reference = (string?)cell.Attribute("r")
                        ?? throw new InvalidDataException($"На листе «{name}» найдена ячейка без адреса.");
                    var columnNumber = ParseColumnNumber(reference);
                    var value = ReadCellValue(cell, sharedStrings, name, reference);
                    if (value.Length != 0)
                        cells[columnNumber] = value;
                }

                if (cells.Count != 0)
                    rows[rowNumber] = cells;
            }

            worksheets.Add(new IsodatExcelWorksheet(name, rows));
        }

        return new IsodatExcelWorkbook(worksheets);
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path)
            ?? throw new InvalidDataException($"В Excel-файле отсутствует часть «{path}».");
        using var stream = entry.Open();
        using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        return XDocument.Load(xmlReader, LoadOptions.None);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null)
            return Array.Empty<string>();

        var xml = LoadXml(archive, "xl/sharedStrings.xml");
        return xml.Root!
            .Elements(MainNamespace + "si")
            .Select(item => string.Concat(item.Descendants(MainNamespace + "t").Select(text => text.Value)))
            .ToArray();
    }

    private static string ReadCellValue(
        XElement cell,
        IReadOnlyList<string> sharedStrings,
        string sheetName,
        string reference)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
            return string.Concat(cell.Descendants(MainNamespace + "t").Select(text => text.Value));

        var rawValue = (string?)cell.Element(MainNamespace + "v") ?? "";
        if (type != "s" || rawValue.Length == 0)
            return rawValue;

        if (!int.TryParse(rawValue, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            || index < 0 || index >= sharedStrings.Count)
            throw new InvalidDataException(
                $"На листе «{sheetName}» ячейка {reference} ссылается на отсутствующий shared string.");

        return sharedStrings[index];
    }

    private static string NormalizeWorksheetPath(string target)
    {
        var normalized = target.Replace('\\', '/').TrimStart('/');
        if (!normalized.StartsWith("xl/", StringComparison.Ordinal))
            normalized = "xl/" + normalized;

        var segments = new List<string>();
        foreach (var segment in normalized.Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count == 0)
                    throw new InvalidDataException("Путь листа выходит за пределы Excel-архива.");
                segments.RemoveAt(segments.Count - 1);
            }
            else if (segment != "." && segment.Length != 0)
            {
                segments.Add(segment);
            }
        }

        return string.Join("/", segments);
    }

    private static int ParseColumnNumber(string cellReference)
    {
        var columnNumber = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character))
                break;

            var upper = char.ToUpperInvariant(character);
            if (upper < 'A' || upper > 'Z')
                throw new InvalidDataException($"Некорректный адрес ячейки «{cellReference}».");
            columnNumber = checked(columnNumber * 26 + upper - 'A' + 1);
        }

        if (columnNumber == 0)
            throw new InvalidDataException($"Некорректный адрес ячейки «{cellReference}».");
        return columnNumber;
    }

    private static int ParsePositiveInteger(string? value, string description)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            && result > 0)
            return result;
        throw new InvalidDataException($"Некорректное значение: {description} «{value}».");
    }
}
