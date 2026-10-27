using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using FloodZoneCalculator.Domain;

// Гиперссылка на основной класс.
#nullable enable

namespace FloodZoneCalculator.Presentation
{
    // ======================================================================
    //  MainForm.Analytics.cs — вкладка «Аналитика».
    //  Слева список графиков, справа — хост, который рисует выбранный график.
    //  Dispatcher: DrawSelectedAnalytics. Реализация графиков 0–5 здесь,
    //  6–11 — в MainForm.AnalyticsMore.cs. Общие помощники (рамка, оси,
    //  столбцы, линии, легенда) — ниже.
    // ======================================================================
    public sealed partial class CalculationForm
    {
        private ListBox _analyticsList = null!;
        private Panel _analyticsHost = null!;

        // Упорядоченный список названий — индекс совпадает с case в диспетчере.
        private static readonly string[] AnalyticsTitles =
        {
            "1. Продольный профиль по створам",
            "2. Затопление по створам (площадь)",
            "3. Водно-энергетический режим по месяцам",
            "4. Расход–напор водосброса (Q–H)",
            "5. Покрытие импорта изодат (тепловая карта)",
            "6. Мощность от расхода (N–Q)",
            "7. Водообеспеченность по месяцам",
            "8. Объёмно-уровенная кривая водохранилища",
            "9. Кривая обеспеченности с интерполяцией",
            "10. Сценарный анализ паводка",
            "11. Профиль характеристик объекта",
            "12. Оценка надёжности (дефицит стока)",
            "13. Русловой профиль: ложе + УВК + УЭК",
            "14. Совмещённый разрез всех изодат",
            "15. Профиль максимумов по створам",
            "16. Живое сечение и периметр створа",
        };

        private Control BuildAnalyticsWorkspace()
        {
            var split = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(4),
                BackColor = Color.Transparent,
            };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330F));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            var caption = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Text = "Аналитические графики",
                Font = ModernTheme.FontBold,
                ForeColor = ModernTheme.PrimaryLight,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 6, 4, 2),
            };
            _analyticsList = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font(ModernTheme.FontMain.FontFamily, 10F),
                IntegralHeight = false,
                BorderStyle = BorderStyle.None,
                BackColor = ModernTheme.Surface,
                ForeColor = ModernTheme.TextMain,
                HorizontalScrollbar = true,
            };
            _analyticsList.Items.AddRange(AnalyticsTitles.Cast<object>().ToArray());
            _analyticsList.SelectedIndexChanged += (_, _) => _analyticsHost?.Invalidate();

            var left = new Panel { Dock = DockStyle.Fill, BackColor = ModernTheme.Panel, Padding = new Padding(6) };
            left.Controls.Add(_analyticsList);
            left.Controls.Add(caption);

            _analyticsHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                Margin = new Padding(6, 0, 0, 0),
                Padding = new Padding(6),
            };
            _analyticsHost.Paint += (_, e) =>
            {
                if (_analyticsList is null) return;
                DrawSelectedAnalytics(e.Graphics, _analyticsHost.ClientRectangle);
            };

            if (_analyticsList.Items.Count > 0) _analyticsList.SelectedIndex = 0;

            split.Controls.Add(left, 0, 0);
            split.Controls.Add(_analyticsHost, 1, 0);
            return split;
        }

        private void DrawSelectedAnalytics(Graphics graphics, Rectangle bounds)
        {
            DrawAnalyticsChart(graphics, bounds, _analyticsList?.SelectedIndex ?? -1);
        }

        // Рисует конкретный аналитический график по индексу в произвольный Graphics
        // (используется и для показа на вкладке, и для рендера в битмап при экспорте).
        private void DrawAnalyticsChart(Graphics graphics, Rectangle bounds, int index)
        {
            graphics.Clear(Color.White);
            if (index < 0)
            {
                EmptyState(graphics, bounds, "Выберите график из списка слева.");
                return;
            }

            var old = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                switch (index)
                {
                    case 0: ChartLongitudinal(graphics, bounds); break;
                    case 1: ChartFlood(graphics, bounds); break;
                    case 2: ChartWaterEnergyMonthly(graphics, bounds); break;
                    case 3: ChartSpillwayRating(graphics, bounds); break;
                    case 4: ChartCoverage(graphics, bounds); break;
                    case 5: ChartPowerCurve(graphics, bounds); break;
                    case 6: ChartWaterUtilization(graphics, bounds); break;
                    case 7: ChartStorageCurve(graphics, bounds); break;
                    case 8: ChartProvisionInterpolation(graphics, bounds); break;
                    case 9: ChartFloodScenario(graphics, bounds); break;
                    case 10: ChartObjectProfileRadar(graphics, bounds); break;
                    case 11: ChartReliability(graphics, bounds); break;
                    case 12: ChartChannelProfile(graphics, bounds); break;
                    case 13: ChartCombinedIzodataSection(graphics, bounds); break;
                    case 14: ChartMaximaProfile(graphics, bounds); break;
                    case 15: ChartWettedSection(graphics, bounds); break;
                    default: EmptyState(graphics, bounds, "График не реализован."); break;
                }
            }
            finally
            {
                graphics.SmoothingMode = old;
            }
        }

        // ------------------------- Общие помощники -------------------------

        // Контекст построения: рамки plotted и границы осей + преобразование данных в пиксели.
        private sealed class PlotCtx
        {
            public Rectangle Plot;
            public double XMin, XMax, YMin, YMax;
            public float MapX(double x) => Plot.Left + (float)((x - XMin) / Math.Max(1e-9, XMax - XMin) * Plot.Width);
            public float MapY(double y) => Plot.Bottom - (float)((y - YMin) / Math.Max(1e-9, YMax - YMin) * Plot.Height);
        }

        // Рисует карточку-фон, градиентное поле, сетку с засечками и подписями по оси Y; возвращает контекст.
        private PlotCtx AnalyticFrame(Graphics g, Rectangle bounds, string title, string subtitle,
            double xMin, double xMax, double yMin, double yMax)
        {
            DrawCardBackground(g, bounds);
            var plot = GetPlotRectangle(bounds, 58, 44, 20, 50);
            var ctx = new PlotCtx { Plot = plot, XMin = xMin, XMax = xMax, YMin = yMin, YMax = yMax };

            using (var bg = new LinearGradientBrush(plot,
                       Color.FromArgb(252, 253, 255), Color.FromArgb(237, 243, 250), LinearGradientMode.Vertical))
                g.FillRectangle(bg, plot);

            using var tf = new Font(Font.FontFamily, 10.5f, FontStyle.Bold);
            using var sf = new Font(Font.FontFamily, 8f, FontStyle.Regular);
            TextRenderer.DrawText(g, title, tf, new Point(plot.Left, bounds.Top + 8), Color.FromArgb(23, 37, 55));
            if (!string.IsNullOrEmpty(subtitle))
                TextRenderer.DrawText(g, subtitle, sf, new Point(plot.Left, bounds.Top + 26), Color.Gray);

            using var grid = new Pen(Color.FromArgb(224, 230, 238));
            using var gridV = new Pen(Color.FromArgb(233, 238, 245));
            using var axis = new Pen(Color.FromArgb(120, 128, 140));
            for (var i = 0; i <= 4; i++)
            {
                var v = yMin + (yMax - yMin) * i / 4.0;
                var y = ctx.MapY(v);
                g.DrawLine(grid, plot.Left, y, plot.Right, y);
                g.DrawLine(axis, plot.Left - 3, y, plot.Left, y);
                TextRenderer.DrawText(g, FormatNum(v), SystemFonts.DefaultFont, new Point(4, (int)y - 8), Color.DimGray);
            }
            for (var i = 0; i <= 4; i++)
            {
                var x = plot.Left + plot.Width * i / 4f;
                g.DrawLine(gridV, x, plot.Top, x, plot.Bottom);
                g.DrawLine(axis, x, plot.Bottom, x, plot.Bottom + 3);
            }
            g.DrawLine(axis, plot.Left, plot.Top, plot.Left, plot.Bottom);
            g.DrawLine(axis, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            return ctx;
        }

        // ------------------------- Помощники «реалистичной» отрисовки -------------------------

        private static void DrawCardBackground(Graphics g, Rectangle bounds)
        {
            var card = Rectangle.Inflate(bounds, -4, -4);
            if (card.Width < 8 || card.Height < 8) return;
            using var path = RoundedRect(card, 10);
            using (var fill = new LinearGradientBrush(
                       new Rectangle(card.Left, card.Top, card.Width, Math.Min(card.Height, 140)),
                       Color.White, Color.FromArgb(246, 248, 251), LinearGradientMode.Vertical))
                g.FillPath(fill, path);
            using var border = new Pen(Color.FromArgb(214, 220, 228), 1f);
            g.DrawPath(border, path);
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Заливка водной толщины вертикальным градиентом (светлее у поверхности).
        private static void WaterFill(Graphics g, PointF[] poly, Rectangle plot)
        {
            if (poly.Length < 3) return;
            var top = poly.Min(p => p.Y);
            var bot = poly.Max(p => p.Y);
            using var lg = new LinearGradientBrush(
                new Rectangle(plot.Left, (int)top, plot.Width, Math.Max(1, (int)(bot - top))),
                Color.FromArgb(150, 150, 205, 235), Color.FromArgb(215, 28, 92, 150), LinearGradientMode.Vertical);
            g.FillPolygon(lg, poly);
        }

        // Универсальная полупрозрачная площадь под кривой с градиентом в цвет серии.
        private static void AreaFill(Graphics g, PointF[] poly, Rectangle plot, Color color)
        {
            if (poly.Length < 3) return;
            var top = poly.Min(p => p.Y);
            var bot = poly.Max(p => p.Y);
            using var lg = new LinearGradientBrush(
                new Rectangle(plot.Left, (int)top, plot.Width, Math.Max(1, (int)(bot - top))),
                Color.FromArgb(130, color), Color.FromArgb(35, color), LinearGradientMode.Vertical);
            g.FillPolygon(lg, poly);
        }

        // Грунт: базовый цвет полигона + диагональная штриховка внутри контура.
        private static void HatchPolygon(Graphics g, PointF[] poly, Color baseColor, Color hatchColor)
        {
            if (poly.Length < 3) return;
            using (var sb = new SolidBrush(baseColor)) g.FillPolygon(sb, poly);
            using var path = new GraphicsPath();
            path.AddPolygon(poly);
            var old = g.Clip;
            g.SetClip(path, CombineMode.Replace);
            using (var hb = new HatchBrush(HatchStyle.LightDownwardDiagonal, hatchColor, Color.Transparent))
            {
                var b = PointsBounds(poly);
                g.FillRectangle(hb, b.Left, b.Top, b.Width, b.Height);
            }
            g.Clip = old;
        }

        private static RectangleF PointsBounds(PointF[] p)
        {
            if (p.Length == 0) return RectangleF.Empty;
            var minx = p.Min(q => q.X); var maxx = p.Max(q => q.X);
            var miny = p.Min(q => q.Y); var maxy = p.Max(q => q.Y);
            return new RectangleF(minx, miny, maxx - minx, maxy - miny);
        }

        // Волнистая линия поверхности воды.
        private static void DrawWaterSurface(Graphics g, float x1, float x2, float y)
        {
            using var p = new Pen(Color.FromArgb(42, 120, 178), 1.8f);
            var prev = new PointF(x1, y);
            for (var x = x1; x <= x2; x += 6f)
            {
                var cur = new PointF(x, y + (float)Math.Sin(x * 0.25) * 1.2f);
                g.DrawLine(p, prev, cur);
                prev = cur;
            }
        }

        // Размерная линия по горизонтали с двумя наконечниками и подписью.
        private static void DimH(Graphics g, float x1, float x2, float y, string text, Color c)
        {
            using var p = new Pen(c, 1f);
            g.DrawLine(p, x1, y, x2, y);
            g.DrawLine(p, x1, y - 4, x1, y + 4);
            g.DrawLine(p, x2, y - 4, x2, y + 4);
            g.DrawLine(p, x1, y, x1 + 6, y - 3); g.DrawLine(p, x1, y, x1 + 6, y + 3);
            g.DrawLine(p, x2, y, x2 - 6, y - 3); g.DrawLine(p, x2, y, x2 - 6, y + 3);
            var sz = TextRenderer.MeasureText(text, SystemFonts.DefaultFont);
            var bx = (x1 + x2) / 2 - sz.Width / 2f;
            g.FillRectangle(Brushes.White, bx, y - 8, sz.Width, 14);
            TextRenderer.DrawText(g, text, SystemFonts.DefaultFont, new Point((int)bx, (int)y - 8), c);
        }

        // Размерная линия по вертикали с наконечниками и подписью справа.
        private static void DimV(Graphics g, float x, float y1, float y2, string text, Color c)
        {
            if (Math.Abs(y1 - y2) < 3) return;
            using var p = new Pen(c, 1f);
            g.DrawLine(p, x, y1, x, y2);
            g.DrawLine(p, x - 4, y1, x + 4, y1); g.DrawLine(p, x - 4, y2, x + 4, y2);
            g.DrawLine(p, x, y1, x - 3, y1 + 6); g.DrawLine(p, x, y1, x + 3, y1 + 6);
            g.DrawLine(p, x, y2, x - 3, y2 - 6); g.DrawLine(p, x, y2, x + 3, y2 - 6);
            var sz = TextRenderer.MeasureText(text, SystemFonts.DefaultFont);
            var ty = (y1 + y2) / 2 - sz.Height / 2f;
            var tx = x + 7;
            g.FillRectangle(Brushes.White, tx, ty, sz.Width, sz.Height);
            TextRenderer.DrawText(g, text, SystemFonts.DefaultFont, new Point((int)tx, (int)ty), c);
        }

        // Пунктирный горизонтальный ориентир с подписью (если значение в диапазоне Y).
        private static void DrawHRef(Graphics g, PlotCtx ctx, double value, Color color, string label)
        {
            if (value <= 0 || value < ctx.YMin || value > ctx.YMax) return;
            var y = ctx.MapY(value);
            using var pen = new Pen(color, 1.4f) { DashStyle = DashStyle.Dash };
            g.DrawLine(pen, ctx.Plot.Left, y, ctx.Plot.Right, y);
            TextRenderer.DrawText(g, label, SystemFonts.DefaultFont, new Point(ctx.Plot.Right - 54, (int)y - 9), color);
        }

        // Сгруппированные вертикальные столбцы по категориям (равномерно по X в пикселях).
        private static void DrawGroupedBars(Graphics g, PlotCtx ctx, string[] categories,
            double[][] series, Color[] colors, string[]? legend)
        {
            var n = categories.Length;
            if (n == 0) return;
            var s = series.Length;
            var group = ctx.Plot.Width / (double)n;
            var bar = Math.Max(2.0, group * 0.72 / Math.Max(1, s));
            var baseY = ctx.MapY(ctx.YMin);
            for (var ci = 0; ci < n; ci++)
            {
                var cx = ctx.Plot.Left + group * (ci + 0.5);
                for (var si = 0; si < s; si++)
                {
                    var v = series[si].Length > ci ? series[si][ci] : 0;
                    if (v < 0) v = 0;
                    var top = ctx.MapY(Math.Min(v, ctx.YMax));
                    var bx = (float)(cx - s * bar / 2 + si * bar);
                    using var brush = new SolidBrush(colors[si]);
                    g.FillRectangle(brush, bx, top, (float)(bar - 1.5), baseY - top);
                }
                if (n <= 16)
                {
                    var rc = new Rectangle((int)(cx - group / 2), ctx.Plot.Bottom + 3, (int)group, 16);
                    TextRenderer.DrawText(g, categories[ci], SystemFonts.DefaultFont, rc,
                        Color.FromArgb(70, 80, 92), TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                }
            }
            if (legend != null) DrawLegend(g, ctx.Plot, legend, colors);
        }

        // Линия по точкам (x,y) с маркерами. Опционально — пунктир.
        private static void DrawLineSeries(Graphics g, PlotCtx ctx, double[] xs, double[] ys,
            Color color, float width, bool dashed)
        {
            using var pen = new Pen(color, width) { DashStyle = dashed ? DashStyle.Dash : DashStyle.Solid };
            var pts = new List<PointF>();
            for (var i = 0; i < xs.Length && i < ys.Length; i++)
                pts.Add(new PointF(ctx.MapX(xs[i]), ctx.MapY(Math.Max(ctx.YMin, Math.Min(ctx.YMax, ys[i])))));
            if (pts.Count > 1) g.DrawLines(pen, pts.ToArray());
            using var marker = new SolidBrush(color);
            foreach (var p in pts) g.FillEllipse(marker, p.X - 2.5f, p.Y - 2.5f, 5, 5);
        }

        private static void DrawLegend(Graphics g, Rectangle plot, string[] names, Color[] colors)
        {
            var x = (float)plot.Left + 6f;
            const float y = 4f;
            for (var i = 0; i < names.Length; i++)
            {
                using var b = new SolidBrush(colors[i]);
                var size = TextRenderer.MeasureText(names[i], SystemFonts.DefaultFont);
                g.FillRectangle(b, x, plot.Top + y, 9, 9);
                TextRenderer.DrawText(g, names[i], SystemFonts.DefaultFont,
                    new Point((int)(x + 12), plot.Top + (int)y - 1), Color.FromArgb(60, 70, 82));
                x += 12 + size.Width + 12;
            }
        }

        private static void EmptyState(Graphics g, Rectangle b, string message)
        {
            TextRenderer.DrawText(g, message, SystemFonts.DefaultFont,
                new Rectangle(b.Left + 20, b.Top + 20, b.Width - 40, b.Height - 40),
                Color.DimGray, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
        }

        private static string FormatNum(double v) => v.ToString("0.###", CultureInfo.CurrentCulture);

        // Округление вверх до «красивого» значения 1/2/5·10^k.
        private static double NiceCeil(double v)
        {
            if (v <= 0) return 1;
            var pow = Math.Pow(10, Math.Floor(Math.Log10(v)));
            var norm = v / pow;
            var nice = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10;
            return nice * pow;
        }

        // Разбор таблицы обеспеченности: (P%, Q).
        private List<(double P, double Q)> ReadProvisionPairs()
        {
            var list = new List<(double, double)>();
            for (var i = 0; i < _flowProvisionGrid.Rows.Count; i++)
            {
                var p = ReadCellNumber(_flowProvisionGrid.Rows[i].Cells[0].Value);
                var q = ReadCellNumber(_flowProvisionGrid.Rows[i].Cells[1].Value);
                if (q > 0) list.Add((p, q));
            }
            return list;
        }

        // Разбор таблицы выработки по месяцам.
        private (string[] Months, double[] Flow, double[] Head, double[] Gen) ReadGenerationSeries()
        {
            var rows = _generationGrid.Rows.Count;
            var months = new string[rows];
            var flow = new double[rows];
            var head = new double[rows];
            var gen = new double[rows];
            for (var i = 0; i < rows; i++)
            {
                months[i] = Convert.ToString(_generationGrid.Rows[i].Cells[0].Value) ?? "";
                flow[i] = ReadCellNumber(_generationGrid.Rows[i].Cells[1].Value);
                head[i] = ReadCellNumber(_generationGrid.Rows[i].Cells[2].Value);
                gen[i] = ReadCellNumber(_generationGrid.Rows[i].Cells[3].Value);
            }
            return (months, flow, head, gen);
        }

        // ------------------------------ Графики 0–5 ------------------------------

        // 1. Продольный профиль по створам (по записям БД с отметками ложа).
        private void ChartLongitudinal(Graphics g, Rectangle b)
        {
            var recs = _databaseCrossSections
                .Where(r => ParseDoubleOrZero(r.BottomLevelZb) != 0 || ParseDoubleOrZero(r.DepthHb) != 0)
                .OrderBy(r => ParseDoubleOrZero(r.DistanceFromHydroUnitM))
                .ToList();
            if (recs.Count == 0)
            {
                EmptyState(g, b, "Нет створов из БД с отметками ложа. Продольный профиль строится по записям базы данных.");
                return;
            }

            var xs = recs.Select(r => ParseDoubleOrZero(r.DistanceFromHydroUnitM)).ToArray();
            if (xs.Max() <= xs.Min()) xs = recs.Select((_, i) => (double)i).ToArray();
            var beds = recs.Select(r =>
            {
                var z = ParseDoubleOrZero(r.BottomLevelZb);
                return z != 0 ? z : -ParseDoubleOrZero(r.DepthHb);
            }).ToArray();

            var upper = ParseDoubleOrZero(_objectProfile.NormalHeadwaterM);
            var lower = ParseDoubleOrZero(_objectProfile.MinWaterLevelM);
            var damHeight = ParseDoubleOrZero(_objectProfile.DamMaxHeightM);
            var crest = beds.Length > 0 && damHeight > 0 ? beds[0] + damHeight : 0;

            var yMin = beds.Min();
            var yMax = beds.Max();
            foreach (var v in new[] { upper, lower, crest })
                if (v > 0) { yMin = Math.Min(yMin, v); yMax = Math.Max(yMax, v); }
            if (yMax - yMin < 1e-6) yMax = yMin + 1;

            var ctx = AnalyticFrame(g, b, "Продольный профиль по створам",
                "расстояние от ГУ, м → отметки, м (ложе; УВБ/НПУ; УНБ; гребень)",
                xs.Min(), xs.Max(), yMin, yMax);

            using (var soil = new SolidBrush(Color.FromArgb(70, 152, 118, 82)))
            {
                var poly = new List<PointF> { new PointF(ctx.MapX(xs[0]), ctx.Plot.Bottom) };
                for (var i = 0; i < xs.Length; i++) poly.Add(new PointF(ctx.MapX(xs[i]), ctx.MapY(beds[i])));
                poly.Add(new PointF(ctx.MapX(xs[xs.Length - 1]), ctx.Plot.Bottom));
                g.FillPolygon(soil, poly.ToArray());
            }
            DrawLineSeries(g, ctx, xs, beds, Color.FromArgb(96, 72, 48), 2f, false);
            DrawHRef(g, ctx, upper, Color.FromArgb(37, 99, 235), "УВБ/НПУ");
            DrawHRef(g, ctx, lower, Color.FromArgb(22, 163, 74), "УНБ");
            DrawHRef(g, ctx, crest, Color.FromArgb(220, 38, 38), "гребень");

            var step = Math.Max(1, xs.Length / 8);
            for (var i = 0; i < xs.Length; i++)
                if (i % step == 0)
                    TextRenderer.DrawText(g, "№" + recs[i].Number, SystemFonts.DefaultFont,
                        new Point((int)ctx.MapX(xs[i]) - 8, ctx.Plot.Bottom + 3), Color.Gray);
        }

        // 2. Площадь затопления по створам (левый/правый берег).
        private void ChartFlood(Graphics g, Rectangle b)
        {
            if (_sections.Count == 0)
            {
                EmptyState(g, b, "Импортируйте створы из Excel, чтобы увидеть площади затопления.");
                return;
            }
            var cats = _sections.Select(s => "№" + s.Number).ToArray();
            var la = _sections.Select(s => s.FloodAreaLeft).ToArray();
            var ra = _sections.Select(s => s.FloodAreaRight).ToArray();
            var yMax = NiceCeil(Math.Max(la.Length > 0 ? la.Max() : 0, ra.Length > 0 ? ra.Max() : 0));
            var ctx = AnalyticFrame(g, b, "Затопление по створам", "площадь затопления, м²",
                0, _sections.Count - 1, 0, yMax);
            DrawGroupedBars(g, ctx, cats, new[] { la, ra },
                new[] { Color.FromArgb(37, 99, 235), Color.FromArgb(245, 158, 11) },
                new[] { "Левый берег", "Правый берег" });
        }

        // 3. Водно-энергетический режим по месяцам (выработка — столбцы, расход — линия).
        private void ChartWaterEnergyMonthly(Graphics g, Rectangle b)
        {
            var (months, flow, _, gen) = ReadGenerationSeries();
            if (gen.All(v => v <= 0) && flow.All(v => v <= 0))
            {
                EmptyState(g, b, "Заполните таблицу выработки (расход / напор / выработка по месяцам).");
                return;
            }
            var yMax = NiceCeil(Math.Max(gen.Max(), flow.Max()));
            var ctx = AnalyticFrame(g, b, "Водно-энергетический режим по месяцам",
                "общая шкала: столбцы — выработка, млн кВт·ч; линия — расход, м³/с",
                0, Math.Max(1, months.Length - 1), 0, yMax);
            var cats = months.Select((m, i) => string.IsNullOrWhiteSpace(m) ? (i + 1).ToString() : m).ToArray();
            DrawGroupedBars(g, ctx, cats, new[] { gen }, new[] { Color.FromArgb(59, 130, 246) }, null);
            var idx = Enumerable.Range(0, flow.Length).Select(i => (double)i).ToArray();
            DrawLineSeries(g, ctx, idx, flow, Color.FromArgb(16, 185, 129), 2f, false);
            DrawLegend(g, ctx.Plot, new[] { "выработка", "расход" },
                new[] { Color.FromArgb(59, 130, 246), Color.FromArgb(16, 185, 129) });
        }

        // 4. Расход–напор водосброса (водосливная формула Q = C·B·H^1.5).
        private void ChartSpillwayRating(Graphics g, Rectangle b)
        {
            var width = Math.Max(1, ParseDoubleOrZero(_objectProfile.DamLengthM));
            var qcap = ParseDoubleOrZero(_objectProfile.SpillwayCapacityM3s);
            const double C = 1.7;
            var href = Math.Max(2, ParseDoubleOrZero(_objectProfile.DamMaxHeightM) * 0.5);

            var hs = new List<double>();
            var qs = new List<double>();
            for (var i = 0; i <= 40; i++)
            {
                var h = href * i / 40.0;
                hs.Add(h);
                qs.Add(C * width * Math.Pow(h, 1.5));
            }
            var yMax = NiceCeil(qs.Max());
            var ctx = AnalyticFrame(g, b, "Расход–напор водосброса (Q–H)",
                "оценка: Q = C·B·H^1.5, C=1.7, B = длина плотины",
                0, href, 0, yMax);
            DrawLineSeries(g, ctx, hs.ToArray(), qs.ToArray(), Color.FromArgb(37, 99, 235), 2.2f, false);

            if (qcap > 0)
            {
                DrawHRef(g, ctx, qcap, Color.FromArgb(220, 38, 38), "Q проект");
                var hc = Math.Pow(qcap / (C * width), 2.0 / 3.0);
                if (hc <= href)
                {
                    using var dot = new Pen(Color.FromArgb(148, 163, 184), 1f) { DashStyle = DashStyle.Dot };
                    var x = ctx.MapX(hc);
                    g.DrawLine(dot, x, ctx.Plot.Top, x, ctx.Plot.Bottom);
                    TextRenderer.DrawText(g, $"H при Qпроект ≈ {hc:0.#} м", SystemFonts.DefaultFont,
                        new Point(ctx.Plot.Left + 6, ctx.Plot.Bottom + 18), Color.Gray);
                }
            }
        }

        // 5. Покрытие импорта изодат — тепловая карта: створы × (тип/берег).
        private void ChartCoverage(Graphics g, Rectangle b)
        {
            if (_sections.Count == 0)
            {
                EmptyState(g, b, "Импортируйте створы из Excel.");
                return;
            }
            var types = new[] { IzodataType.Depth, IzodataType.Velocity, IzodataType.DryingCalculated, IzodataType.DryingFactual };
            var cols = new[] { "Глуб L", "Глуб R", "Скор L", "Скор R", "Осуш расч L", "Осуш расч R", "Осуш факт L", "Осуш факт R" };
            var rows = _sections.Count;
            var nc = cols.Length;

            var rect = GetPlotRectangle(b, 92, 40, 12, 24);
            var cw = rect.Width / (float)nc;
            var ch = Math.Min(32f, rect.Height / (float)rows);

            using var grid = new Pen(Color.FromArgb(180, 188, 198));
            using var ok = new SolidBrush(Color.FromArgb(178, 226, 190));
            using var bad = new SolidBrush(Color.FromArgb(245, 198, 198));

            for (var c = 0; c < nc; c++)
            {
                var rc = new Rectangle((int)(rect.Left + c * cw), rect.Top - 20, (int)cw, 18);
                TextRenderer.DrawText(g, cols[c], SystemFonts.DefaultFont, rc, Color.FromArgb(50, 60, 72),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }
            for (var r = 0; r < rows; r++)
            {
                var sec = _sections[r];
                TextRenderer.DrawText(g, "№" + sec.Number, SystemFonts.DefaultFont,
                    new Rectangle(rect.Left - 60, rect.Top + (int)(r * ch), 56, (int)ch),
                    Color.FromArgb(40, 50, 62), TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
                for (var c = 0; c < nc; c++)
                {
                    var type = types[c / 2];
                    var isLeft = c % 2 == 0;
                    var count = sec.GetPoints(isLeft ? Bank.Left : Bank.Right, type).Count;
                    var cell = new RectangleF(rect.Left + c * cw, rect.Top + r * ch, cw, ch);
                    g.FillRectangle(count > 0 ? ok : bad, cell);
                    var rx = Rectangle.Round(cell);
                    g.DrawRectangle(grid, rx);
                    TextRenderer.DrawText(g, count.ToString(), SystemFonts.DefaultFont, rx,
                        Color.FromArgb(30, 40, 50), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
            TextRenderer.DrawText(g, "число точек в типе/береге (зелёный — есть данные)", SystemFonts.DefaultFont,
                new Point(rect.Left, rect.Top + (int)(rows * ch) + 6), Color.Gray);
        }

        // 6. Мощность от расхода: N = ρ·g·Q·H·η (оценка).
        private void ChartPowerCurve(Graphics g, Rectangle b)
        {
            var upper = ParseDoubleOrZero(_objectProfile.NormalHeadwaterM);
            var lower = ParseDoubleOrZero(_objectProfile.MinWaterLevelM);
            var damHeight = ParseDoubleOrZero(_objectProfile.DamMaxHeightM);
            var head = upper > lower && upper > 0 ? upper - lower : damHeight;
            if (head <= 0)
            {
                EmptyState(g, b, "Нужен напор: заполните УВБ/УНБ или высоту плотины.");
                return;
            }
            var eff = ParseDoubleOrZero(_objectProfile.EfficiencyPct);
            eff = eff <= 0 ? 85 : eff;
            if (eff > 1) eff /= 100.0;
            eff = Math.Min(0.95, Math.Max(0.5, eff));

            var qmax = ParseDoubleOrZero(_objectProfile.MaxFlowM3s);
            if (qmax <= 0) qmax = Math.Max(10, ParseDoubleOrZero(_objectProfile.SpillwayCapacityM3s));
            if (qmax <= 0) qmax = 100;

            var qs = new List<double>();
            var ns = new List<double>();
            for (var i = 0; i <= 50; i++)
            {
                var q = qmax * i / 50.0;
                qs.Add(q);
                ns.Add(9.81 * q * head * eff / 1000.0); // МВт
            }
            var yMax = NiceCeil(ns.Max());
            var ctx = AnalyticFrame(g, b, "Мощность от расхода (N–Q)",
                $"оценка: N = ρg·Q·H·η; H ≈ {head:0.#} м, η ≈ {eff:0.##}", 0, qmax, 0, yMax);
            DrawLineSeries(g, ctx, qs.ToArray(), ns.ToArray(), Color.FromArgb(220, 38, 38), 2.2f, false);
            var design = ParseDoubleOrZero(_objectProfile.DesignPowerMw);
            if (design > 0) DrawHRef(g, ctx, design, Color.FromArgb(37, 99, 235), "N устр");
        }
    }
}
