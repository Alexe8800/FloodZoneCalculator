#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

// Разрешаем неоднозначность Font/Color между System.Drawing и Wordprocessing.
using Color = System.Drawing.Color;
using Font = System.Drawing.Font;
using WColor = DocumentFormat.OpenXml.Wordprocessing.Color;

namespace FloodZoneCalculator.Presentation.ReportExport;

// =====================================================================================
//  Модель содержимого отчёта.
//  Отчёт = титульный лист (ReportTitlePage) + упорядоченный набор элементов
//  (ReportElement): графики (ReportChart), таблицы (ReportTable) и текстовые блоки
//  (ReportText). Все разделы формируются автоматически из данных расчёта — пользователю
//  остаётся только отметить, что включить.
// =====================================================================================

/// <summary>Базовый элемент отчёта с заголовком (подписью).</summary>
public abstract class ReportElement
{
    protected ReportElement(string title) => Title = title ?? "";
    public string Title { get; }
}

/// <summary>График: подпись + уже отрендеренное изображение.</summary>
public sealed class ReportChart : ReportElement
{
    public ReportChart(string title, Bitmap image) : base(title) => Image = image;
    public Bitmap Image { get; }
}

/// <summary>Таблица: заголовок, шапка колонок и строки данных.</summary>
public sealed class ReportTable : ReportElement
{
    public ReportTable(string title, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
        : base(title)
    {
        Headers = headers;
        Rows = rows;
    }
    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<string[]> Rows { get; }
}

/// <summary>Текстовый раздел: заголовок + набор абзацев.</summary>
public sealed class ReportText : ReportElement
{
    public ReportText(string title, IReadOnlyList<string> paragraphs) : base(title)
        => Paragraphs = paragraphs;
    public IReadOnlyList<string> Paragraphs { get; }
}

/// <summary>Титульный лист отчёта.</summary>
public sealed class ReportTitlePage
{
    public string Organization { get; set; } = "";   // эксплуатирующая организация / собственник
    public string ObjectName { get; set; } = "";      // название гидроузла / плотины
    public string DocumentTitle { get; set; } = "";   // «Отчёт по расчёту зоны затопления…»
    public string Subtitle { get; set; } = "";        // река, тип сооружения
    public string Location { get; set; } = "";        // регион/населённый пункт
    public string Year { get; set; } = "";            // год формирования
    public string DateText { get; set; } = "";        // дата расчёта
    public List<KeyValuePair<string, string>> Facts { get; } = new(); // ключевые реквизиты
}

/// <summary>Итоговое содержимое отчёта: титул + элементы.</summary>
public sealed class ReportContent
{
    public ReportTitlePage TitlePage { get; set; } = new();
    public List<ReportElement> Elements { get; } = new();
}

// =====================================================================================
//  Экспорт в Word (.docx) через DocumentFormat.OpenXml (без внешних зависимостей).
//  Титульный лист → разрыв страницы → элементы (таблицы с рамкой, графики, текст).
// =====================================================================================
public static class WordReportExporter
{
    // EMU на пиксель при 96 dpi и максимальная ширина иллюстрации под поле A4.
    private const long EmuPerPixel = 9525L;
    private const long MaxContentWidthEmu = 5_400_000L;

    public static void Export(string filePath, ReportContent content)
    {
        using var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document(new Body());
        var body = main.Document.Body!;

        AppendTitlePage(body, content.TitlePage);

        var figure = 1;
        var tableNo = 1;
        foreach (var element in content.Elements)
        {
            // Каждый элемент — с новой страницы, чтобы подпись не отрывать от данных.
            body.Append(PageBreak());
            switch (element)
            {
                case ReportTable table:
                    body.Append(Heading("Таблица " + tableNo + ". " + table.Title, 12, bold: true, center: false));
                    body.Append(BuildTable(table));
                    body.Append(Spacer());
                    tableNo++;
                    break;

                case ReportText text:
                    body.Append(Heading(text.Title, 14, bold: true, center: false));
                    if (text.Paragraphs.Count == 0)
                        body.Append(BodyParagraph("—"));
                    foreach (var line in text.Paragraphs)
                        body.Append(BodyParagraph(line));
                    body.Append(Spacer());
                    break;

                case ReportChart chart:
                    body.Append(Heading("Рисунок " + figure + ". " + chart.Title, 12, bold: true, center: false));
                    body.Append(BuildImageParagraph(main, chart.Image));
                    body.Append(Spacer());
                    figure++;
                    break;
            }
        }

        body.Append(BuildSectionProperties());
        main.Document.Save();
    }

    // ------------------------------------------------------------------ титульный лист
    private static void AppendTitlePage(Body body, ReportTitlePage t)
    {
        if (!string.IsNullOrWhiteSpace(t.Organization))
            body.Append(Heading(t.Organization, 12, bold: false, center: true));

        //Vertical offset — a few empty lines to push the block down the page.
        for (var i = 0; i < 4; i++) body.Append(Spacer());

        if (!string.IsNullOrWhiteSpace(t.ObjectName))
            body.Append(Heading(t.ObjectName, 20, bold: true, center: true));
        if (!string.IsNullOrWhiteSpace(t.DocumentTitle))
            body.Append(Heading(t.DocumentTitle, 15, bold: true, center: true));
        if (!string.IsNullOrWhiteSpace(t.Subtitle))
            body.Append(Heading(t.Subtitle, 12, bold: false, center: true));

        body.Append(Spacer());

        if (t.Facts.Count > 0)
            body.Append(BuildFactsTable(t.Facts));

        for (var i = 0; i < 5; i++) body.Append(Spacer());

        var place = string.Join(" ", new[] { t.Location, t.Year }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(place))
            body.Append(Heading(place, 12, bold: false, center: true));
        if (!string.IsNullOrWhiteSpace(t.DateText))
            body.Append(Heading(t.DateText, 10, bold: false, center: true));
    }

    // Двухколоночная таблица реквизитов без видимой сетки (только разделители строк).
    private static Table BuildFactsTable(List<KeyValuePair<string, string>> facts)
    {
        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableWidth { Width = "7000", Type = TableWidthUnitValues.Dxa },
            new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 2, Color = "C7D2DE" },
                new InsideVerticalBorder { Val = BorderValues.None })));

        foreach (var fact in facts)
        {
            var row = new TableRow(
                Cell(fact.Key, widthDxa: "2800", bold: true, align: JustificationValues.Left),
                Cell(fact.Value, widthDxa: "4200", bold: false, align: JustificationValues.Left));
            table.AppendChild(row);
        }
        return table;
    }

    // ------------------------------------------------------------------ таблица раздела
    private static Table BuildTable(ReportTable report)
    {
        var cols = report.Headers.Count;
        const int totalDxa = 9746; // поле A4 за вычетом полей 2,54 см
        var widthPer = cols == 0 ? totalDxa : totalDxa / cols;

        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "94A3B8" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "94A3B8" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "94A3B8" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "94A3B8" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "CBD5E1" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "CBD5E1" }),
            new TableWidth { Width = totalDxa.ToString(), Type = TableWidthUnitValues.Dxa }));

        // Шапка
        var headerRow = new TableRow(new TableRowProperties(new TableHeader()));
        foreach (var header in report.Headers)
            headerRow.AppendChild(Cell(header, widthDxa: widthPer.ToString(), bold: true,
                align: JustificationValues.Center, shading: "0F3B57", textColor: "FFFFFF"));
        table.AppendChild(headerRow);

        // Данные
        foreach (var dataRow in report.Rows)
        {
            var row = new TableRow();
            for (var c = 0; c < cols; c++)
            {
                var value = c < dataRow.Length ? dataRow[c] : "";
                var align = c == 0 ? JustificationValues.Left : JustificationValues.Center;
                row.AppendChild(Cell(value, widthDxa: widthPer.ToString(), bold: false, align: align));
            }
            table.AppendChild(row);
        }
        return table;
    }

    private static TableCell Cell(string text, string widthDxa, bool bold,
        JustificationValues align, string? shading = null, string? textColor = null)
    {
        var run = new Run(new Text(text ?? "") { Space = SpaceProcessingModeValues.Preserve });
        var props = new RunProperties(
            new RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI" },
            new FontSize { Val = "18" });
        if (bold) props.Append(new Bold());
        props.Append(new WColor { Val = textColor ?? "1F2937" });
        run.PrependChild(props);

        var paragraph = new Paragraph(run);
        paragraph.PrependChild(new ParagraphProperties(new Justification { Val = align }));

        var cellProps = new TableCellProperties(new TableCellWidth { Width = widthDxa, Type = TableWidthUnitValues.Dxa });
        if (shading != null)
            cellProps.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = shading });

        return new TableCell(cellProps, paragraph);
    }

    // ------------------------------------------------------------------ график
    private static Paragraph BuildImageParagraph(MainDocumentPart main, Bitmap image)
    {
        var bytes = ToPng(image);
        var imagePart = main.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(bytes))
            imagePart.FeedData(stream);
        var relationshipId = main.GetIdOfPart(imagePart);

        long cx = image.Width * EmuPerPixel;
        long cy = image.Height * EmuPerPixel;
        if (cx > MaxContentWidthEmu)
        {
            cy = (long)((double)cy * MaxContentWidthEmu / cx);
            cx = MaxContentWidthEmu;
        }

        var drawing = BuildInlineImage(relationshipId, cx, cy, imagePart.Uri.OriginalString);
        var paragraph = new Paragraph(new Run(drawing));
        paragraph.PrependChild(new ParagraphProperties(
            new Justification { Val = JustificationValues.Center }));
        return paragraph;
    }

    private static byte[] ToPng(Bitmap image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    private static Drawing BuildInlineImage(string relationshipId, long widthEmu, long heightEmu, string name)
    {
        return new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = 1U, Name = name },
                new DW.NonVisualGraphicFrameDrawingProperties(
                    new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = name },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relationshipId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                new A.PresetGeometry { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })));
    }

    // ------------------------------------------------------------------ мелочь
    private static Paragraph PageBreak() =>
        new Paragraph(new Run(new Break { Type = BreakValues.Page }));

    private static Paragraph Heading(string text, double sizePt, bool bold, bool center)
    {
        var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        var props = new RunProperties(
            new RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI" },
            new FontSize { Val = ((int)(sizePt * 2)).ToString() },
            new WColor { Val = bold ? "0F3B57" : "1F2937" });
        if (bold) props.Append(new Bold());
        run.PrependChild(props);

        var paragraph = new Paragraph(run);
        if (center)
            paragraph.PrependChild(new ParagraphProperties(
                new Justification { Val = JustificationValues.Center }));
        return paragraph;
    }

    private static Paragraph BodyParagraph(string text)
    {
        var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        run.PrependChild(new RunProperties(
            new RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI" },
            new FontSize { Val = "22" },
            new WColor { Val = "1F2937" }));
        return new Paragraph(run);
    }

    private static Paragraph Spacer() =>
        new Paragraph(new Run(new Text("") { Space = SpaceProcessingModeValues.Preserve }));

    private static SectionProperties BuildSectionProperties()
    {
        // A4 книжная, поля 2,54 см (1440 twips).
        return new SectionProperties(
            new PageSize { Width = 11906U, Height = 16838U },
            new PageMargin { Top = 1080, Right = 1080, Bottom = 1080, Left = 1080 });
    }
}

// =====================================================================================
//  Экспорт в PDF средствами Windows: печать через PrintDocument на «Microsoft Print to PDF».
//  Титульная страница → элементы: графики (страница на график), таблицы и текст (пагинация).
// =====================================================================================
public static class PdfReportExporter
{
    private const string PdfPrinterName = "Microsoft Print to PDF";

    /// <summary>Возвращает true, если пользователь подтвердил печать.</summary>
    public static bool Export(IWin32Window owner, ReportContent content)
    {
        using var printer = new ReportPrinter(content);
        using var dialog = new PrintDialog
        {
            Document = printer.Document,
            UseEXDialog = true,
            AllowSomePages = false,
        };
        TrySelectPdfPrinter(printer.Document);

        if (dialog.ShowDialog(owner) != DialogResult.OK) return false;

        printer.Document.Print();
        return true;
    }

    private static void TrySelectPdfPrinter(PrintDocument document)
    {
        try
        {
            if (PrinterSettings.InstalledPrinters
                    .Cast<string>()
                    .Any(p => string.Equals(p, PdfPrinterName, StringComparison.OrdinalIgnoreCase)))
                document.PrinterSettings.PrinterName = PdfPrinterName;
        }
        catch { /* если список принтеров недоступен — оставляем системный по умолчанию */ }
    }

    // Машина печати: титул → элементы (график/таблица/текст), при необходимости несколько страниц.
    private sealed class ReportPrinter : IDisposable
    {
        private enum Stage { Title, Elements, Done }

        private readonly ReportContent _content;
        private Stage _stage = Stage.Title;
        private int _elementIndex;
        private int _figure = 1;
        private int _tableNo = 1;
        // курсоры внутри текущего табличного/текстового элемента
        private int _rowCursor;
        private int _paraCursor;
        // Пикселей на пункт для текущего устройства печати: шрифты задаём в пикселях,
        // чтобы TextRenderer рисовал глифы в той же системе координат, что и раскладка.
        private float _pxPerPoint = 1f;

        // Шрифт заданного кегля (в пунктах), переведённого в пиксели текущего устройства.
        private Font FontPt(float points, FontStyle style = FontStyle.Regular,
            string family = "Segoe UI") =>
            new Font(family, points * _pxPerPoint, style, GraphicsUnit.Pixel);

        public PrintDocument Document { get; }

        public ReportPrinter(ReportContent content)
        {
            _content = content;
            Document = new PrintDocument
            {
                DocumentName = "FloodZoneReport",
                DefaultPageSettings = { Landscape = true, Margins = new Margins(60, 60, 60, 60) },
            };
            Document.PrintPage += OnPrintPage;
        }

        public void Dispose() => Document.Dispose();

        private void OnPrintPage(object? sender, PrintPageEventArgs e)
        {
            var g = e.Graphics!;

            // Приводим печать к единой системе координат — пикселям устройства.
            // e.MarginBounds приходит в сотых долях дюйма (PrinterUnit), а TextRenderer
            // рисует текст в пикселях. Без перевода строки таблицы получают крошечный шаг
            // и текст вылезает за границы ячеек (особенно у «Microsoft Print to PDF» с его
            // высоким разрешением). Переводим область печати в пиксели и переключаем PageUnit.
            g.PageUnit = GraphicsUnit.Pixel;
            _pxPerPoint = g.DpiY / 72f;
            var mb = e.MarginBounds;
            var area = new Rectangle(
                (int)(mb.X / 100f * g.DpiX),
                (int)(mb.Y / 100f * g.DpiY),
                (int)(mb.Width / 100f * g.DpiX),
                (int)(mb.Height / 100f * g.DpiY));

            if (_stage == Stage.Title)
            {
                DrawTitlePage(g, area, _content.TitlePage);
                _stage = Stage.Elements;
                e.HasMorePages = _content.Elements.Count > 0;
                if (!e.HasMorePages) _stage = Stage.Done;
                return;
            }

            if (_stage == Stage.Elements)
            {
                if (_elementIndex >= _content.Elements.Count)
                {
                    e.HasMorePages = false;
                    _stage = Stage.Done;
                    return;
                }

                bool finished = true;
                switch (_content.Elements[_elementIndex])
                {
                    case ReportChart chart:
                        DrawChart(g, area, chart, _figure++);
                        break;
                    case ReportTable table:
                        finished = DrawTable(g, area, table, _tableNo);
                        if (finished) _tableNo++;
                        break;
                    case ReportText text:
                        finished = DrawText(g, area, text);
                        break;
                }

                if (finished)
                {
                    _elementIndex++;
                    _rowCursor = 0;
                    _paraCursor = 0;
                }
                e.HasMorePages = _elementIndex < _content.Elements.Count;
                return;
            }

            e.HasMorePages = false;
        }

        // ------------------------------------------------------------------ титульник
        private void DrawTitlePage(Graphics g, Rectangle area, ReportTitlePage t)
        {
            using var orgFont = FontPt(11f);
            using var nameFont = FontPt(26f, FontStyle.Bold);
            using var titleFont = FontPt(16f, FontStyle.Bold);
            using var subFont = FontPt(12f);
            using var factFont = FontPt(11f);
            using var bottomFont = FontPt(12f);

            using var dark = new SolidBrush(Color.FromArgb(15, 59, 87));
            using var text = new SolidBrush(Color.FromArgb(31, 41, 55));

            float cx = area.Left + area.Width / 2f;
            float y = area.Top;

            if (!string.IsNullOrWhiteSpace(t.Organization))
            {
                y = DrawCentered(g, t.Organization, orgFont, text, cx, y) + 4;
            }

            y += area.Height * 0.16f;

            if (!string.IsNullOrWhiteSpace(t.ObjectName))
                y = DrawCentered(g, t.ObjectName, nameFont, dark, cx, y) + 10;
            if (!string.IsNullOrWhiteSpace(t.DocumentTitle))
                y = DrawCentered(g, t.DocumentTitle, titleFont, dark, cx, y) + 6;
            if (!string.IsNullOrWhiteSpace(t.Subtitle))
                y = DrawCentered(g, t.Subtitle, subFont, text, cx, y) + 18;

            // Реквизиты — две колонки по центру
            foreach (var fact in t.Facts)
            {
                var line = fact.Key + ":   " + fact.Value;
                y = DrawCentered(g, line, factFont, text, cx, y) + 3;
            }

            var place = string.Join(" ", new[] { t.Location, t.Year }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (!string.IsNullOrWhiteSpace(place))
                DrawCentered(g, place, bottomFont, dark, cx, area.Bottom - 60);
            if (!string.IsNullOrWhiteSpace(t.DateText))
                DrawCentered(g, t.DateText, subFont, text, cx, area.Bottom - 34);
        }

        private static float DrawCentered(Graphics g, string s, Font font, Brush brush, float cx, float y)
        {
            var size = g.MeasureString(s, font);
            g.DrawString(s, font, brush, cx - size.Width / 2f, y);
            return y + size.Height;
        }

        // ------------------------------------------------------------------ график
        private void DrawChart(Graphics g, Rectangle area, ReportChart chart, int number)
        {
            using var captionFont = FontPt(11f, FontStyle.Bold);
            using var captionBrush = new SolidBrush(Color.FromArgb(15, 59, 87));
            var caption = "Рисунок " + number + ". " + chart.Title;
            var captionSize = g.MeasureString(caption, captionFont);
            g.DrawString(caption, captionFont, captionBrush, area.Left, area.Top);

            var pictureTop = area.Top + (int)captionSize.Height + 8;
            var pictureArea = new Rectangle(area.Left, pictureTop, area.Width, area.Bottom - pictureTop);
            DrawImageFit(g, chart.Image, pictureArea);
        }

        // ------------------------------------------------------------------ таблица (пагинация)
        private bool DrawTable(Graphics g, Rectangle area, ReportTable table, int number)
        {
            int cols = table.Headers.Count;
            if (cols == 0) return true;

            using var headFont = FontPt(11f, FontStyle.Bold);
            using var captionFont = FontPt(11f, FontStyle.Bold);
            using var cellFont = FontPt(10f);
            using var dark = new SolidBrush(Color.FromArgb(15, 59, 87));
            using var headBack = new SolidBrush(Color.FromArgb(15, 59, 87));
            using var headText = new SolidBrush(Color.White);
            using var grid = new Pen(Color.FromArgb(203, 213, 225));
            using var cellText = new SolidBrush(Color.FromArgb(31, 41, 55));

            float y = area.Top;

            // Заголовок таблицы — только на первой странице этого элемента.
            if (_rowCursor == 0)
            {
                g.DrawString("Таблица " + number + ". " + table.Title, captionFont, dark, area.Left, y);
                y += g.MeasureString("Таблица " + number + ". " + table.Title, captionFont).Height + 6;
            }

            // Ширины колонок пропорционально максимальной длине текста.
            var weights = new float[cols];
            for (var c = 0; c < cols; c++)
            {
                float w = g.MeasureString(table.Headers[c], cellFont).Width;
                foreach (var row in table.Rows)
                    if (c < row.Length)
                        w = Math.Max(w, g.MeasureString(row[c] ?? "", cellFont).Width);
                weights[c] = Math.Min(Math.Max(w + 12, 60), area.Width * 0.6f);
            }
            var total = weights.Sum();
            var scale = area.Width / total;
            var widths = weights.Select(w => w * scale).ToArray();

            var rowH = cellFont.GetHeight(g) + 8;

            // Шапка — на каждой странице элемента.
            DrawTableRow(g, area, table.Headers, widths, ref y, rowH, headBack, headText, cellFont, bold: true);

            int printed = 0;
            int maxPerThisPage = Math.Max(1, (int)((area.Bottom - y) / rowH));

            for (; _rowCursor < table.Rows.Count; _rowCursor++)
            {
                if (printed >= maxPerThisPage)
                    return false; // продолжим на следующей странице
                DrawTableRow(g, area, table.Rows[_rowCursor], widths, ref y, rowH, null, cellText, cellFont, bold: false);
                printed++;
            }
            return true;
        }

        private static void DrawTableRow(Graphics g, Rectangle area, IReadOnlyList<string> values, float[] widths,
            ref float y, float rowH, Brush? back, Brush text, Font font, bool bold)
        {
            using var fontBold = bold ? new Font(font, FontStyle.Bold) : null;
            var useFont = fontBold ?? font;

            var x = (float)area.Left;
            var rect = new RectangleF(area.Left, y, area.Width, rowH);
            if (back != null) g.FillRectangle(back, rect);

            for (var c = 0; c < widths.Length; c++)
            {
                var value = c < values.Count ? values[c] : "";
                var cellRect = new RectangleF(x + 4, y + 3, widths[c] - 8, rowH - 4);
                var flags = c == 0
                    ? TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis
                    : TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                TextRenderer.DrawText(g, value, useFont, Rectangle.Round(cellRect),
                    back == null ? Color.FromArgb(31, 41, 55) : Color.White, flags);
                g.DrawLine(Pens.LightSteelBlue, x, y, x, y + rowH);
                x += widths[c];
            }
            g.DrawLine(Pens.LightSteelBlue, x, y, x, y + rowH);
            g.DrawLine(Pens.LightSteelBlue, area.Left, y + rowH, area.Right, y + rowH);
            y += rowH;
        }

        // ------------------------------------------------------------------ текст (пагинация)
        private bool DrawText(Graphics g, Rectangle area, ReportText text)
        {
            using var headFont = FontPt(15f, FontStyle.Bold);
            using var font = FontPt(11f);
            using var dark = new SolidBrush(Color.FromArgb(15, 59, 87));
            using var brush = new SolidBrush(Color.FromArgb(31, 41, 55));

            float y = area.Top;
            if (_paraCursor == 0)
            {
                g.DrawString(text.Title, headFont, dark, area.Left, y);
                y += g.MeasureString(text.Title, headFont).Height + 8;
            }

            var lineHeight = font.GetHeight(g) + 6;
            for (; _paraCursor < text.Paragraphs.Count; _paraCursor++)
            {
                var para = string.IsNullOrWhiteSpace(text.Paragraphs[_paraCursor]) ? " " : text.Paragraphs[_paraCursor];
                var bounds = new RectangleF(area.Left, y, area.Width, lineHeight * 6);
                var size = TextRenderer.MeasureText(g, para, font,
                    new Size(area.Width, int.MaxValue), TextFormatFlags.WordBreak);
                if (y + size.Height > area.Bottom) return false;
                TextRenderer.DrawText(g, para, font, new Rectangle(area.Left, (int)y, area.Width, size.Height),
                    Color.FromArgb(31, 41, 55), TextFormatFlags.WordBreak);
                y += size.Height + 4;
            }
            return true;
        }

        private static void DrawImageFit(Graphics g, Image image, Rectangle area)
        {
            var scale = Math.Min((double)area.Width / image.Width, (double)area.Height / image.Height);
            var w = (int)(image.Width * scale);
            var h = (int)(image.Height * scale);
            var x = area.Left + (area.Width - w) / 2;
            var y = area.Top + (area.Height - h) / 2;
            g.DrawImage(image, new Rectangle(x, y, w, h));
        }
    }
}
