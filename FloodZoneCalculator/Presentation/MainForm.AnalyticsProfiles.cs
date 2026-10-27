using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;

// Гиперссылка на основной класс.
#nullable enable

namespace FloodZoneCalculator.Presentation
{
    // ======================================================================
    //  MainForm.AnalyticsProfiles.cs — дополнительные ПРОФИЛИ (графики 13–16)
    //  для вкладки «Аналитика». Переиспользуют помощники из MainForm.Analytics.cs
    //  (AnalyticFrame, DrawLineSeries, DrawGroupedBars, DrawLegend, NiceCeil,
    //  EmptyState, FormatNum). Здесь же — приватные помощники по створам.
    // ======================================================================
    public sealed partial class CalculationForm
    {
        private const double Gravity = 9.81;

        // ---- Помощники по данным створов ----

        // Выбранный створ (из дерева на вкладке створов), иначе — первый из импортированных.
        private CrossSection? SelectedSection()
        {
            var s = _sectionsTree?.SelectedNode?.Tag as CrossSection;
            if (s == null && _sectionsTree?.SelectedNode?.Tag is CrossSectionRecord rec)
                s = _sections.FirstOrDefault(z => z.Number == rec.Number);
            s ??= _sections.FirstOrDefault();
            return s;
        }

        // Зеркальные точки типа по обоим берегам: левый → −расстояние, правый → +расстояние; сортировка по X.
        private static List<(double x, double v)> MirrorPoints(CrossSection s, IzodataType type)
        {
            var list = new List<(double, double)>();
            foreach (var p in s.GetPoints(Bank.Left, type)) list.Add((-p.Distance, p.Value));
            foreach (var p in s.GetPoints(Bank.Right, type)) list.Add((p.Distance, p.Value));
            return list.OrderBy(q => q.Item1).ToList();
        }

        private static double SectionMaxDepth(CrossSection s)
        {
            var m = 0.0;
            foreach (var p in s.GetPoints(Bank.Left, IzodataType.Depth)) if (p.Value > m) m = p.Value;
            foreach (var p in s.GetPoints(Bank.Right, IzodataType.Depth)) if (p.Value > m) m = p.Value;
            return m;
        }

        private static double SectionMaxVelocity(CrossSection s)
        {
            var m = 0.0;
            foreach (var p in s.GetPoints(Bank.Left, IzodataType.Velocity)) if (p.Value > m) m = p.Value;
            foreach (var p in s.GetPoints(Bank.Right, IzodataType.Velocity)) if (p.Value > m) m = p.Value;
            return m;
        }

        // ------------------------------ Графики ------------------------------

        // 13. Русловой профиль: ложе + уровень воды (УВК) + линия удельной энергии (УЭК).
        private void ChartChannelProfile(Graphics g, Rectangle b)
        {
            if (_sections.Count == 0)
            {
                EmptyState(g, b, "Импортируйте створы из Excel.");
                return;
            }
            var secs = _sections.OrderBy(s => s.Number).ToList();
            var n = secs.Count;
            var xs = new double[n];
            var bed = new double[n];
            var ws = new double[n];
            var en = new double[n];
            var haveLevel = secs.Any(s => s.WaterLevel > 0 || s.MaxLevel > 0);
            var haveDist = secs.Any(s => s.DistanceFromDam > 0);
            for (var i = 0; i < n; i++)
            {
                var s = secs[i];
                xs[i] = haveDist ? s.DistanceFromDam : i;
                var depth = s.MaxDepth > 0 ? s.MaxDepth : (s.Depth > 0 ? s.Depth : SectionMaxDepth(s));
                var vel = s.MaxVelocity > 0 ? s.MaxVelocity : (s.Velocity > 0 ? s.Velocity : SectionMaxVelocity(s));
                var level = haveLevel ? (s.WaterLevel > 0 ? s.WaterLevel : s.MaxLevel) : 0;
                ws[i] = level;
                bed[i] = level - depth;
                en[i] = level + 1.1 * vel * vel / (2 * Gravity);
            }
            var yMin = bed.Min();
            var yMax = Math.Max(ws.Max(), en.Max());
            if (yMax - yMin < 1e-6) yMax = yMin + 1;
            var xMin = xs.Min();
            var xMax = xs.Max();
            if (xMax - xMin < 1e-6) { xMin = 0; xMax = Math.Max(1, n - 1); }

            var ctx = AnalyticFrame(g, b, "Русловой профиль: ложе + УВК + УЭК",
                "отметки, м (условно от поверхности, если нет абсолютных уровней)",
                xMin, xMax, yMin, yMax);

            if (n >= 2)
            {
                var bedPix = new PointF[n];
                var wsPix = new PointF[n];
                for (var i = 0; i < n; i++)
                {
                    bedPix[i] = new PointF(ctx.MapX(xs[i]), ctx.MapY(bed[i]));
                    wsPix[i] = new PointF(ctx.MapX(xs[i]), ctx.MapY(ws[i]));
                }
                var wpoly = new List<PointF>(bedPix);
                for (var i = n - 1; i >= 0; i--) wpoly.Add(wsPix[i]);
                WaterFill(g, wpoly.ToArray(), ctx.Plot);
                var epoly = new List<PointF>(bedPix)
                {
                    new PointF(bedPix[n - 1].X, ctx.Plot.Bottom),
                    new PointF(bedPix[0].X, ctx.Plot.Bottom),
                };
                HatchPolygon(g, epoly.ToArray(), Color.FromArgb(202, 174, 134), Color.FromArgb(122, 94, 60));
            }
            DrawLineSeries(g, ctx, xs, bed, Color.FromArgb(92, 68, 44), 2f, false);
            DrawLineSeries(g, ctx, xs, ws, Color.FromArgb(37, 99, 235), 2f, false);
            DrawLineSeries(g, ctx, xs, en, Color.FromArgb(220, 38, 38), 1.6f, true);
            DrawLegend(g, ctx.Plot, new[] { "ложе", "УВК (уровень воды)", "УЭК (уд. энергия)" },
                new[] { Color.FromArgb(96, 72, 48), Color.FromArgb(37, 99, 235), Color.FromArgb(220, 38, 38) });

            var step = Math.Max(1, n / 8);
            for (var i = 0; i < n; i++)
                if (i % step == 0)
                    TextRenderer.DrawText(g, "№" + secs[i].Number, SystemFonts.DefaultFont,
                        new Point((int)ctx.MapX(xs[i]) - 8, ctx.Plot.Bottom + 3), Color.Gray);
        }

        // 14. Совмещённый поперечный разрез: все типы изодат на одном поле (общая шкала значений).
        private void ChartCombinedIzodataSection(Graphics g, Rectangle b)
        {
            var s = SelectedSection();
            if (s == null)
            {
                EmptyState(g, b, "Выберите створ (вкладка створов) или импортируйте Excel.");
                return;
            }
            var types = new[] { IzodataType.Depth, IzodataType.Velocity, IzodataType.DryingCalculated, IzodataType.DryingFactual };
            var colors = new[]
            {
                Color.FromArgb(37, 99, 235), Color.FromArgb(16, 185, 129),
                Color.FromArgb(245, 158, 11), Color.FromArgb(220, 38, 38),
            };
            var seriesX = new List<double[]>();
            var seriesY = new List<double[]>();
            var names = new List<string>();
            var used = new List<Color>();
            var xMin = 0.0; var xMax = 1.0; var yMax = 1.0;
            for (var k = 0; k < types.Length; k++)
            {
                var mp = MirrorPoints(s, types[k]);
                if (mp.Count == 0) continue;
                seriesX.Add(mp.Select(q => q.x).ToArray());
                seriesY.Add(mp.Select(q => q.v).ToArray());
                names.Add(GetIzodataTypeName(types[k]));
                used.Add(colors[k]);
                xMin = Math.Min(xMin, mp.Min(q => q.x));
                xMax = Math.Max(xMax, mp.Max(q => q.x));
                yMax = Math.Max(yMax, mp.Max(q => q.v));
            }
            if (seriesX.Count == 0)
            {
                EmptyState(g, b, $"Створ №{s.Number}: нет точек изодат для разреза.");
                return;
            }
            var ctx = AnalyticFrame(g, b, $"Створ №{s.Number} — все изодаты на разрезе",
                "расстояние от фарватера, м → значение (общая шкала)",
                xMin, xMax, 0, NiceCeil(yMax));
            using (var th = new Pen(Color.FromArgb(148, 163, 184), 1f) { DashStyle = DashStyle.Dash })
                g.DrawLine(th, ctx.MapX(0), ctx.Plot.Top, ctx.MapX(0), ctx.Plot.Bottom);
            for (var i = 0; i < seriesX.Count; i++)
                DrawLineSeries(g, ctx, seriesX[i], seriesY[i], used[i], 2f, false);
            DrawLegend(g, ctx.Plot, names.ToArray(), used.ToArray());
        }

        // 15. Профиль максимумов по створам: глубина (левая ось) и скорость (правая ось).
        private void ChartMaximaProfile(Graphics g, Rectangle b)
        {
            if (_sections.Count == 0)
            {
                EmptyState(g, b, "Импортируйте створы из Excel.");
                return;
            }
            var secs = _sections.OrderBy(s => s.Number).ToList();
            var n = secs.Count;
            var xs = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
            var depth = secs.Select(s => s.MaxDepth > 0 ? s.MaxDepth : SectionMaxDepth(s)).ToArray();
            var vel = secs.Select(s => s.MaxVelocity > 0 ? s.MaxVelocity : SectionMaxVelocity(s)).ToArray();
            var leftMax = NiceCeil(depth.Max());
            if (leftMax <= 0) leftMax = 1;
            var vMax = NiceCeil(Math.Max(vel.Max(), 0.1));
            if (vMax <= 0) vMax = 1;

            var ctx = AnalyticFrame(g, b, "Профиль максимумов по створам",
                "левая ось — глубина, м; правая ось — скорость, м/с",
                0, Math.Max(1, n - 1), 0, leftMax);
            var velScaled = vel.Select(v => v / vMax * leftMax).ToArray();
            DrawLineSeries(g, ctx, xs, depth, Color.FromArgb(37, 99, 235), 2.2f, false);
            DrawLineSeries(g, ctx, xs, velScaled, Color.FromArgb(220, 38, 38), 2f, true);

            using (var rp = new Pen(Color.FromArgb(220, 38, 38)))
                g.DrawLine(rp, ctx.Plot.Right, ctx.Plot.Top, ctx.Plot.Right, ctx.Plot.Bottom);
            for (var i = 0; i <= 4; i++)
            {
                var vv = vMax * i / 4.0;
                var y = ctx.MapY(vv / vMax * leftMax);
                TextRenderer.DrawText(g, FormatNum(vv), SystemFonts.DefaultFont,
                    new Point(ctx.Plot.Right + 4, (int)y - 8), Color.FromArgb(180, 40, 40));
            }
            DrawLegend(g, ctx.Plot, new[] { "глубина (макс)", "скорость (макс)" },
                new[] { Color.FromArgb(37, 99, 235), Color.FromArgb(220, 38, 38) });
            var step = Math.Max(1, n / 10);
            for (var i = 0; i < n; i++)
                if (i % step == 0)
                    TextRenderer.DrawText(g, "№" + secs[i].Number, SystemFonts.DefaultFont,
                        new Point((int)ctx.MapX(xs[i]) - 8, ctx.Plot.Bottom + 3), Color.Gray);
        }

        // 16. Живое сечение створа: профиль глубины + площадь ω, периметр χ, радиус R, ширина B.
        private void ChartWettedSection(Graphics g, Rectangle b)
        {
            var s = SelectedSection();
            if (s == null)
            {
                EmptyState(g, b, "Выберите створ или импортируйте Excel.");
                return;
            }
            var pts = MirrorPoints(s, IzodataType.Depth);
            if (pts.Count < 2)
            {
                EmptyState(g, b, $"Створ №{s.Number}: нет точек глубины для живого сечения.");
                return;
            }
            var cnt = pts.Count;
            var x = new double[cnt];
            var d = new double[cnt];
            for (var i = 0; i < cnt; i++) { x[i] = pts[i].x; d[i] = Math.Max(0, pts[i].v); }

            var width = x[cnt - 1] - x[0];
            var area = 0.0; var per = 0.0;
            for (var i = 0; i < cnt - 1; i++)
            {
                var dx = x[i + 1] - x[i];
                area += (d[i] + d[i + 1]) / 2 * dx;
                per += Math.Sqrt(dx * dx + (d[i + 1] - d[i]) * (d[i + 1] - d[i]));
            }
            var radius = per > 0 ? area / per : 0;
            var meanDepth = width > 0 ? area / width : 0;
            var dMax = Math.Max(0.5, d.Max());
            var xMin = x[0]; var xMax = x[cnt - 1];
            if (xMax - xMin < 1e-6) xMax = xMin + 1;

            var plot = GetPlotRectangle(b, 64, 44, 24, 66);
            using (var tf = new Font(Font.FontFamily, 10f, FontStyle.Bold))
                TextRenderer.DrawText(g, $"Створ №{s.Number} — живое сечение", tf,
                    new Point(plot.Left, b.Top + 6), Color.FromArgb(31, 41, 55));
            using (var sf = new Font(Font.FontFamily, 8f, FontStyle.Regular))
                TextRenderer.DrawText(g, "глубина вниз от поверхности воды", sf,
                    new Point(plot.Left, b.Top + 24), Color.Gray);

            float MapX(double v) => plot.Left + (float)((v - xMin) / (xMax - xMin) * plot.Width);
            float MapYd(double v) => plot.Top + (float)(v / dMax * plot.Height);

            var bp = Enumerable.Range(0, cnt).Select(i => new PointF(MapX(x[i]), MapYd(d[i]))).ToArray();
            var waterPoly = new List<PointF> { new PointF(bp[0].X, plot.Top) };
            waterPoly.AddRange(bp);
            waterPoly.Add(new PointF(bp[cnt - 1].X, plot.Top));
            WaterFill(g, waterPoly.ToArray(), plot);
            var earthPoly = new List<PointF>(bp)
            {
                new PointF(bp[cnt - 1].X, plot.Bottom),
                new PointF(bp[0].X, plot.Bottom),
            };
            HatchPolygon(g, earthPoly.ToArray(), Color.FromArgb(202, 174, 134), Color.FromArgb(122, 94, 60));
            using (var bedPen = new Pen(Color.FromArgb(92, 68, 44), 2f)) g.DrawLines(bedPen, bp);
            DrawWaterSurface(g, plot.Left, plot.Right, plot.Top);
            if (xMin < 0 && xMax > 0)
                using (var th = new Pen(Color.FromArgb(148, 163, 184), 1f) { DashStyle = DashStyle.Dash })
                {
                    var fx = MapX(0);
                    g.DrawLine(th, fx, plot.Top, fx, plot.Bottom);
                }
            using (var axis = new Pen(Color.FromArgb(110, 118, 128)))
            {
                g.DrawLine(axis, plot.Left, plot.Top, plot.Left, plot.Bottom);
                g.DrawLine(axis, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            }
            for (var i = 0; i <= 4; i++)
            {
                var vv = dMax * i / 4.0;
                TextRenderer.DrawText(g, FormatNum(vv), SystemFonts.DefaultFont,
                    new Point(2, (int)MapYd(vv) - 8), Color.DimGray);
            }
            var stats = $"ω = {area:0.##} м²    χ = {per:0.##} м    R = ω/χ = {radius:0.###} м    B = {width:0.#} м    hср = {meanDepth:0.##} м";
            TextRenderer.DrawText(g, stats, Font, new Point(plot.Left, plot.Bottom + 10), Color.FromArgb(30, 40, 50));
        }
    }
}
