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
    //  MainForm.AnalyticsMore.cs — графики 6–11 вкладки «Аналитика».
    //  Использует общие помощники из MainForm.Analytics.cs (AnalyticFrame,
    //  DrawGroupedBars, DrawLineSeries, DrawHRef, NiceCeil, EmptyState,
    //  ReadProvisionPairs, ReadGenerationSeries).
    // ======================================================================
    public sealed partial class CalculationForm
    {
        // 7. Водообеспеченность по месяцам: относительная выработка на единицу расхода.
        private void ChartWaterUtilization(Graphics g, Rectangle b)
        {
            var (months, flow, _, gen) = ReadGenerationSeries();
            var ratio = new double[months.Length];
            var has = false;
            for (var i = 0; i < months.Length; i++)
            {
                if (flow[i] > 0 && gen[i] > 0)
                {
                    ratio[i] = gen[i] / flow[i];
                    has = true;
                }
            }
            if (!has)
            {
                EmptyState(g, b, "Нужны заполненные расход и выработка по месяцам.");
                return;
            }
            var yMax = NiceCeil(ratio.Max());
            var ctx = AnalyticFrame(g, b, "Водообеспеченность по месяцам",
                "относительная эффективность = выработка / расход (условные единицы)",
                0, Math.Max(1, months.Length - 1), 0, yMax);
            var cats = months.Select((m, i) => string.IsNullOrWhiteSpace(m) ? (i + 1).ToString() : m).ToArray();
            DrawGroupedBars(g, ctx, cats, new[] { ratio }, new[] { Color.FromArgb(124, 58, 237) }, null);
            var avg = ratio.Where(v => v > 0).Average();
            DrawHRef(g, ctx, avg, Color.FromArgb(220, 38, 38), "среднее");
        }

        // 8. Объёмно-уровенная кривая водохранилища (параметрическая оценка).
        private void ChartStorageCurve(Graphics g, Rectangle b)
        {
            var zDead = ParseDoubleOrZero(_objectProfile.DeadVolumeLevelM);
            var zNorm = ParseDoubleOrZero(_objectProfile.NormalHeadwaterM);
            var full = ParseDoubleOrZero(_objectProfile.ReservoirVolumeMlnM3);
            var useful = ParseDoubleOrZero(_objectProfile.ReservoirUsefulVolumeMlnM3);
            if (zNorm <= 0 || full <= 0 || zNorm <= zDead)
            {
                EmptyState(g, b, "Нужны: НПУ (NormalHeadwater), полный объём и мёртвый уровень. Это параметрическая оценка объём-уровень.");
                return;
            }
            var zs = new List<double>();
            var vs = new List<double>();
            for (var i = 0; i <= 40; i++)
            {
                var t = i / 40.0;
                var z = zDead + (zNorm - zDead) * t;
                zs.Add(z);
                vs.Add(full * Math.Pow(t, 1.3)); // лёгкая выпуклость: площадь растёт с уровнем
            }
            var ctx = AnalyticFrame(g, b, "Объёмно-уровенная кривая водохранилища",
                "оценка (параметрическая): V = Vполн·((Z−Zмёртв)/(NPU−Zмёртв))^1.3",
                zDead, zNorm, 0, NiceCeil(full));
            DrawLineSeries(g, ctx, zs.ToArray(), vs.ToArray(), Color.FromArgb(37, 99, 235), 2.2f, false);
            if (useful > 0 && useful < full)
                DrawHRef(g, ctx, full - useful, Color.FromArgb(245, 158, 11), "мёртвый объём");

            var dl = ctx.MapX(zDead);
            var nl = ctx.MapX(zNorm);
            using (var pen = new Pen(Color.FromArgb(148, 163, 184), 1f) { DashStyle = DashStyle.Dot })
            {
                g.DrawLine(pen, dl, ctx.Plot.Top, dl, ctx.Plot.Bottom);
                g.DrawLine(pen, nl, ctx.Plot.Top, nl, ctx.Plot.Bottom);
            }
            TextRenderer.DrawText(g, "З мёртв", SystemFonts.DefaultFont, new Point((int)dl - 6, ctx.Plot.Bottom + 3), Color.Gray);
            TextRenderer.DrawText(g, "НПУ", SystemFonts.DefaultFont, new Point((int)nl - 6, ctx.Plot.Bottom + 3), Color.Gray);
        }

        // 9. Кривая обеспеченности с интерполяцией и экстраполяцией.
        private void ChartProvisionInterpolation(Graphics g, Rectangle b)
        {
            var pairs = ReadProvisionPairs().OrderBy(p => p.P).ToList();
            if (pairs.Count < 2)
            {
                EmptyState(g, b, "Нужно минимум две точки в таблице обеспеченности (P%, Q).");
                return;
            }
            var ps = pairs.Select(p => p.P).ToArray();
            var qs = pairs.Select(p => p.Q).ToArray();
            var yMax = NiceCeil(qs.Max() * 1.15);
            var ctx = AnalyticFrame(g, b, "Кривая обеспеченности с экстраполяцией",
                "P, % → Q, м³/с (точки — ввод; пунктир — экстраполяция к P=0 и P=100)",
                0, 100, 0, yMax);

            // Экстраполяция влево (к P=0) и вправо (к P=100) по крайним сегментам.
            var last = pairs.Count - 1;
            double Slope(int a, int c) => (qs[c] - qs[a]) / Math.Max(1e-9, ps[c] - ps[a]);
            var leftQ = qs[0] - Slope(0, 1) * ps[0];
            DrawLineSeries(g, ctx, new[] { 0.0, (double)ps[0] }, new[] { Math.Max(0, leftQ), qs[0] },
                Color.FromArgb(148, 163, 184), 1.6f, true);
            var rightQ = qs[last] + Slope(last - 1, last) * (100 - ps[last]);
            DrawLineSeries(g, ctx, new[] { (double)ps[last], 100.0 }, new[] { qs[last], Math.Max(0, rightQ) },
                Color.FromArgb(148, 163, 184), 1.6f, true);

            DrawLineSeries(g, ctx, ps, qs, Color.FromArgb(37, 99, 235), 2.2f, false);
            DrawLegend(g, ctx.Plot, new[] { "обеспеченность Q(P)" }, new[] { Color.FromArgb(37, 99, 235) });
        }

        // 10. Сценарный анализ паводка: Q-сценарии → требуемый напор и относительное затопление.
        private void ChartFloodScenario(Graphics g, Rectangle b)
        {
            var width = Math.Max(1, ParseDoubleOrZero(_objectProfile.DamLengthM));
            var qcap = ParseDoubleOrZero(_objectProfile.SpillwayCapacityM3s);
            var areaBase = _sections.Count > 0 ? _sections.Sum(s => s.FloodAreaLeft + s.FloodAreaRight) : 0;
            var baseQ = qcap > 0 ? qcap : ParseDoubleOrZero(_objectProfile.MaxFlowM3s);
            if (baseQ <= 0 && areaBase <= 0)
            {
                EmptyState(g, b, "Нужна проектная ёмкость водосброса или данные створов для сценариев паводка.");
                return;
            }
            const double C = 1.7;
            var labels = new[] { "0.5×", "1×", "2×", "3×", "5×" };
            var factors = new[] { 0.5, 1.0, 2.0, 3.0, 5.0 };
            var heads = new double[labels.Length];
            var areas = new double[labels.Length];
            for (var i = 0; i < labels.Length; i++)
            {
                var q = baseQ * factors[i];
                heads[i] = Math.Pow(q / (C * width), 2.0 / 3.0);
                areas[i] = areaBase > 0 ? areaBase * Math.Pow(factors[i], 0.8) : 0;
            }
            var yMax = NiceCeil(Math.Max(heads.Max(), 1));
            var ctx = AnalyticFrame(g, b, "Сценарный анализ паводка",
                "множитель расхода → требуемый напор H, м (оценка Q=C·B·H^1.5)",
                0, labels.Length - 1, 0, yMax);
            DrawGroupedBars(g, ctx, labels, new[] { heads }, new[] { Color.FromArgb(220, 38, 38) }, null);

            var lines = new List<string>();
            if (areaBase > 0)
                lines.Add($"оценка площади затопления при 1× ≈ {areaBase:0} м²; растёт как Q^0.8");
            lines.Add($"базовый Q ≈ {baseQ:0} м³/с, B ≈ {width:0} м");
            TextRenderer.DrawText(g, string.Join("   ", lines), SystemFonts.DefaultFont,
                new Point(ctx.Plot.Left, ctx.Plot.Bottom + 20), Color.Gray);
        }

        // 11. Профиль характеристик объекта (нормированный — условные максимумы).
        private void ChartObjectProfileRadar(Graphics g, Rectangle b)
        {
            // Нормируем к условным «типовым максимумам», чтобы показывать относительную величину.
            var names = new[] { "Высота", "Длина", "Водосброс Q", "Мощность", "Выработка", "Объём" };
            var refs = new[] { 100.0, 1000.0, 5000.0, 300.0, 1500.0, 500.0 };
            var raw = new[]
            {
                ParseDoubleOrZero(_objectProfile.DamMaxHeightM),
                ParseDoubleOrZero(_objectProfile.DamLengthM),
                ParseDoubleOrZero(_objectProfile.SpillwayCapacityM3s),
                ParseDoubleOrZero(_objectProfile.DesignPowerMw),
                ParseDoubleOrZero(_objectProfile.AverageAnnualGenerationMkwh),
                ParseDoubleOrZero(_objectProfile.ReservoirVolumeMlnM3),
            };
            if (raw.All(v => v <= 0))
            {
                EmptyState(g, b, "Заполните числовые характеристики объекта (высота, мощность, объём и т.д.).");
                return;
            }
            var pct = new double[names.Length];
            for (var i = 0; i < names.Length; i++)
                pct[i] = refs[i] > 0 ? Math.Min(100, raw[i] / refs[i] * 100.0) : 0;
            var ctx = AnalyticFrame(g, b, "Профиль характеристик объекта",
                "заполненные значения, % от условного типового максимума",
                0, names.Length - 1, 0, 100);
            DrawGroupedBars(g, ctx, names, new[] { pct }, new[] { Color.FromArgb(14, 165, 233) }, null);
            for (var i = 0; i < names.Length; i++)
                if (pct[i] > 0)
                    TextRenderer.DrawText(g, raw[i].ToString("0.#", CultureInfo.CurrentCulture),
                        SystemFonts.DefaultFont,
                        new Point(ctx.Plot.Left + (int)(ctx.Plot.Width / (double)names.Length * (i + 0.5)) - 12,
                            (int)ctx.MapY(pct[i]) - 16), Color.FromArgb(40, 50, 62));
        }

        // 12. Оценка надёжности: кривая Q(P) и уровень минимального стока → зона дефицита.
        private void ChartReliability(Graphics g, Rectangle b)
        {
            var pairs = ReadProvisionPairs().OrderBy(p => p.P).ToList();
            var demand = ParseDoubleOrZero(_objectProfile.MinFlowM3s);
            if (pairs.Count < 2 || demand <= 0)
            {
                EmptyState(g, b, "Нужны кривая обеспеченности (≥2 точек) и минимальный сток (MinFlow).");
                return;
            }
            var ps = pairs.Select(p => p.P).ToArray();
            var qs = pairs.Select(p => p.Q).ToArray();
            var yMax = NiceCeil(Math.Max(qs.Max(), demand) * 1.1);
            var ctx = AnalyticFrame(g, b, "Оценка надёжности (дефицит стока)",
                " shaded — область, где Q(P) ниже минимального стока",
                0, 100, 0, yMax);

            // Штриховка дефицита: там, где кривая проходит ниже demand.
            using (var deficit = new SolidBrush(Color.FromArgb(60, 220, 38, 38)))
            {
                var poly = new List<PointF>();
                for (var i = 0; i < ps.Length; i++)
                {
                    var low = Math.Min(qs[i], demand);
                    poly.Add(new PointF(ctx.MapX(ps[i]), ctx.MapY(low)));
                }
                for (var i = ps.Length - 1; i >= 0; i--)
                    poly.Add(new PointF(ctx.MapX(ps[i]), ctx.MapY(demand)));
                if (poly.Count > 2) g.FillPolygon(deficit, poly.ToArray());
            }

            DrawLineSeries(g, ctx, ps, qs, Color.FromArgb(37, 99, 235), 2.2f, false);
            DrawHRef(g, ctx, demand, Color.FromArgb(220, 38, 38), "Q мин");

            var coef = ParseDoubleOrZero(_objectProfile.ReliabilityCoefficient);
            var above = ps.Where((p, i) => qs[i] >= demand).Count();
            var text = $"точек с обеспеченным стоком: {above}/{ps.Length}" +
                       (coef > 0 ? $"  ·  коэфф. надёжности = {coef:0.###}" : "");
            TextRenderer.DrawText(g, text, SystemFonts.DefaultFont,
                new Point(ctx.Plot.Left, ctx.Plot.Bottom + 20), Color.FromArgb(40, 50, 62));
        }
    }
}
