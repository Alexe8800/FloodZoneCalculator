#nullable enable

using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation
{
    // =====================================================================================
    // MainForm.Drawing.cs — векторная отрисовка (GDI+) в блоке «Hydraulic profile».
    //
    //   DrawHydraulicProfile     — точка входа (Paint), включает сглаживание и вызывает схему.
    //   DrawHydroNodeScheme      — продольный разрез гидроузла: бьефы, плотина (бетон/насыпь),
    //                              водослив, ГЭС, размеры, шкала отметок, легенда.
    //   Draw*Profile/Database    — отрисовка реального профиля створа из БД или из Excel-изодат.
    //   Вспомогательные примитивы: волнистая поверхность воды, стрелки, подписи, легенда.
    //
    // Все размеры привязаны к фактическим отметкам из _objectProfile, поэтому схема
    // масштабируется по высоте реальному перепаду «дно → гребень».
    // =====================================================================================
    public sealed partial class CalculationForm
    {
        // Главная отрисовка гидроузла: включает AntiAlias/качественную интерполяцию и
        // передаёт управление DrawHydroNodeScheme, после чего восстанавливает режимы Graphics.
        private void DrawHydraulicProfile(Graphics graphics, Rectangle bounds)
        {
            graphics.Clear(Color.White);
            if (bounds.Width < 180 || bounds.Height < 130)
                return;
            var oldPixel = graphics.PixelOffsetMode;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var oldSmooth = graphics.SmoothingMode;
            var oldInterp = graphics.InterpolationMode;
            var oldText = graphics.TextRenderingHint;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            try
            {
                DrawHydroNodeScheme(graphics, bounds);
            }
            finally
            {
                graphics.SmoothingMode = oldSmooth;
                graphics.InterpolationMode = oldInterp;
                graphics.TextRenderingHint = oldText;
                graphics.PixelOffsetMode = oldPixel;
            }
        }

        // Прорисовка продольного разреза гидроузла. Отметки берутся из _objectProfile,
        // тип плотины определяет форму тела (бетонная гравитационная или насыпная).
        private void DrawHydroNodeScheme(Graphics graphics, Rectangle bounds)
{
    var hasUpper = TryParseDecimal(_objectProfile.NormalHeadwaterM, out var upperValue);
    var hasLower = TryParseDecimal(_objectProfile.MinWaterLevelM, out var lowerValue);
    var hasBed = TryParseDecimal(_objectProfile.DeadVolumeLevelM, out var bedValue);
    var bedLevel = hasBed ? (double)bedValue : 0;
    var upper = hasUpper ? (double)upperValue : bedLevel + 10;
    if (upper <= bedLevel) upper = bedLevel + 10;
    var lower = hasLower ? (double)lowerValue : bedLevel + (upper - bedLevel) * 0.35;
    if (lower < bedLevel || lower > upper) lower = bedLevel + (upper - bedLevel) * 0.35;
    var damHeight = ParseDoubleOrZero(_objectProfile.DamMaxHeightM);
    var crest = damHeight > 0 ? bedLevel + damHeight : upper + Math.Max(1.0, (upper - bedLevel) * 0.12);
    if (crest < upper + 0.5) crest = upper + Math.Max(1.0, (upper - bedLevel) * 0.10);

    var span = Math.Max(1.0, crest - bedLevel);
    var yMin = bedLevel - span * 0.16;
    var yMax = crest + span * 0.22;

    const int axisLeft = 52;
    const int rightPad = 14;
    const int topPad = 42;
    const int bottomPad = 54;
    var plot = new RectangleF(bounds.Left + axisLeft, bounds.Top + topPad,
        Math.Max(80, bounds.Width - axisLeft - rightPad),
        Math.Max(70, bounds.Height - topPad - bottomPad));

    float Y(double level) => plot.Bottom - (float)((level - yMin) / Math.Max(0.01, yMax - yMin) * plot.Height);

    var bedY = Y(bedLevel);
    var crestY = Y(crest);
    var upperY = Y(upper);
    var lowerY = Y(lower);
    var footY = Math.Min(plot.Bottom - 1f, Y(bedLevel - span * 0.08));

    // --- геометрия тела плотины (заложения: горизонталь / вертикаль) ---
    var embankment = IsEmbankmentDam(_objectProfile.DamType);
    var damCenter = plot.Left + plot.Width * 0.5f;
    var crestHalf = Math.Max(9f, plot.Width * 0.04f);
    var faceHeight = Math.Max(12f, bedY - crestY);
    var upSlope = faceHeight * (embankment ? 2.5f : 0.10f);
    var downSlope = faceHeight * (embankment ? 2.0f : 0.75f);
    var upFaceTopX = damCenter - crestHalf;
    var downFaceTopX = damCenter + crestHalf;
    var maxLeft = upFaceTopX - (plot.Left + 4);
    var maxRight = (plot.Right - 4) - downFaceTopX;
    var scale = Math.Min(1f, Math.Min(maxLeft / Math.Max(1f, upSlope), maxRight / Math.Max(1f, downSlope)));
    if (scale < 0f) scale = 0f;
    upSlope *= scale;
    downSlope *= scale;
    var upToeX = upFaceTopX - upSlope;
    var downToeX = downFaceTopX + downSlope;

    // X точки на откосе на заданной высоте экрана y
    float FaceX(float topX, float toeX, float y)
    {
        var t = (y - crestY) / Math.Max(0.01f, bedY - crestY);
        t = Math.Max(0f, Math.Min(1f, t));
        return topX + (toeX - topX) * t;
    }

    var upFaceAtWater = FaceX(upFaceTopX, upToeX, upperY);
    var downFaceAtWater = FaceX(downFaceTopX, downToeX, lowerY);

    using var titleFont = new Font(Font.FontFamily, 10f, FontStyle.Bold);
    using var labelFont = new Font(Font.FontFamily, 8.25f, FontStyle.Regular);
    using var smallFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);

    var waterDeep = Color.FromArgb(66, 133, 187);
    var waterTop = Color.FromArgb(158, 209, 238);
    var dimColor = Color.FromArgb(70, 80, 92);

    // --- грунт ---
    using (var soil = new SolidBrush(Color.FromArgb(152, 118, 82)))
    using (var soilHatch = new HatchBrush(HatchStyle.LightUpwardDiagonal,
        Color.FromArgb(120, 92, 60), Color.FromArgb(160, 124, 86)))
    using (var soilEdge = new Pen(Color.FromArgb(96, 72, 48), 1.4f))
    {
        var ground = new RectangleF(plot.Left, bedY, plot.Width, plot.Bottom - bedY);
        if (ground.Height > 1)
        {
            graphics.FillRectangle(soil, ground);
            graphics.FillRectangle(soilHatch, ground);
        }
        graphics.DrawLine(soilEdge, plot.Left, bedY, plot.Right, bedY);
    }

    // --- вода: полигоны, ограниченные откосами плотины ---
    if (bedY - upperY > 1f)
    {
        var poly = new[]
        {
            new PointF(plot.Left, upperY), new PointF(upFaceAtWater, upperY),
            new PointF(upToeX, bedY), new PointF(plot.Left, bedY)
        };
        using var br = new LinearGradientBrush(new RectangleF(plot.Left, upperY, plot.Width, bedY - upperY),
            waterTop, waterDeep, LinearGradientMode.Vertical);
        graphics.FillPolygon(br, poly);
    }
    if (bedY - lowerY > 1f)
    {
        var poly = new[]
        {
            new PointF(downFaceAtWater, lowerY), new PointF(plot.Right, lowerY),
            new PointF(plot.Right, bedY), new PointF(downToeX, bedY)
        };
        using var br = new LinearGradientBrush(new RectangleF(plot.Left, lowerY, plot.Width, bedY - lowerY),
            waterTop, waterDeep, LinearGradientMode.Vertical);
        graphics.FillPolygon(br, poly);
    }

    // --- тело плотины (одна согласованная форма, фундамент в метрах) ---
    var damPoly = new[]
    {
        new PointF(upFaceTopX, crestY),
        new PointF(downFaceTopX, crestY),
        new PointF(downToeX, bedY),
        new PointF(downToeX + 3, footY),
        new PointF(upToeX - 3, footY),
        new PointF(upToeX, bedY)
    };
    var damBox = new RectangleF(upToeX - 6, crestY, (downToeX - upToeX) + 12, footY - crestY);
    if (damBox.Height > 1)
    {
        using var face = new LinearGradientBrush(damBox,
            Color.FromArgb(176, 184, 194), Color.FromArgb(104, 112, 124), LinearGradientMode.Vertical);
        using var outline = new Pen(Color.FromArgb(58, 68, 82), 1.8f);
        graphics.FillPolygon(face, damPoly);
        graphics.DrawPolygon(outline, damPoly);
    }

    if (embankment)
    {
        using var core = new SolidBrush(Color.FromArgb(198, 158, 122));
        using var coreEdge = new Pen(Color.FromArgb(120, 92, 60), 1f);
        var cw = Math.Max(3f, crestHalf * 0.5f);
        var corePoly = new[]
        {
            new PointF(damCenter - cw, crestY),
            new PointF(damCenter + cw, crestY),
            new PointF(damCenter + cw * 0.6f, footY),
            new PointF(damCenter - cw * 0.6f, footY)
        };
        graphics.FillPolygon(core, corePoly);
        graphics.DrawPolygon(coreEdge, corePoly);
    }
    else
    {
        var gateCount = Math.Max(2, Math.Min(6, (int)ParseDoubleOrZero(_objectProfile.SpillwayOpeningsCount)));
        var gateTop = crestY + 6;
        var gateBottom = Math.Min(bedY - 3f, upperY + (bedY - upperY) * 0.5f);
        if (gateBottom > gateTop + 3)
        {
            using var gateFill = new SolidBrush(Color.FromArgb(228, 236, 244));
            using var gateLine = new Pen(Color.FromArgb(58, 68, 82), 1.1f);
            var usable = Math.Max(6f, downFaceTopX - upFaceTopX - 6f);
            for (var i = 1; i <= gateCount; i++)
            {
                var center = upFaceTopX + 3 + usable * i / (gateCount + 1);
                var gw = Math.Min(12f, usable / (gateCount + 1) - 2f);
                if (gw < 2) gw = 2;
                var gate = new RectangleF(center - gw / 2, gateTop, gw, gateBottom - gateTop);
                graphics.FillRectangle(gateFill, gate);
                graphics.DrawRectangle(gateLine, gate.X, gate.Y, gate.Width, gate.Height);
            }
        }
    }

    using (var curb = new Pen(Color.FromArgb(72, 82, 96), 2f))
        graphics.DrawLine(curb, upFaceTopX - 4, crestY - 3, downFaceTopX + 4, crestY - 3);

    // --- поверхность воды (оценочные уровни — пунктиром) ---
    DrawWaterSurface(graphics, plot.Left, upFaceAtWater, upperY, Color.FromArgb(42, 120, 178), 1.6f);
    DrawWaterSurface(graphics, downFaceAtWater, plot.Right, lowerY, Color.FromArgb(42, 120, 178), 1.3f);
    using (var est = new Pen(Color.FromArgb(120, 42, 120, 178), 1f) { DashStyle = DashStyle.Dash })
    {
        if (!hasUpper) graphics.DrawLine(est, plot.Left, upperY, upFaceAtWater, upperY);
        if (!hasLower) graphics.DrawLine(est, downFaceAtWater, lowerY, plot.Right, lowerY);
    }

    // --- стрелки потока: от/до откосов ---
    var flowColor = Color.FromArgb(37, 99, 235);
    using (var flow = new Pen(flowColor, 2f))
    {
        var midY = (upperY + bedY) / 2f;
        var upEnd = FaceX(upFaceTopX, upToeX, midY) - 2f;
        if (upEnd - (plot.Left + 6) > 14)
        {
            graphics.DrawLine(flow, plot.Left + 6, midY, upEnd - 4, midY);
            DrawArrowHead(graphics, new PointF(upEnd, midY), 0, 6f, flowColor);
        }
        var lowY = (lowerY + bedY) / 2f;
        var dnStart = FaceX(downFaceTopX, downToeX, lowY) + 2f;
        var dnEnd = Math.Min(plot.Right - 4, downToeX + 18);
        if (dnEnd - dnStart > 14)
        {
            graphics.DrawLine(flow, dnStart, lowY, dnEnd - 4, lowY);
            DrawArrowHead(graphics, new PointF(dnEnd, lowY), 0, 6f, flowColor);
        }
    }

    // --- ГЭС (справа оставлен зазор под размер ΔН) ---
    var phW = Math.Min(plot.Width * 0.15f, 54f);
    var phLeft = Math.Min(downToeX + 6, plot.Right - phW - 18);
    var phTop = bedY - Math.Max(16f, faceHeight * 0.42f);
    if (phLeft > damCenter && phTop > crestY && plot.Right - phLeft > 20)
    {
        var ph = new RectangleF(phLeft, phTop, phW, bedY - phTop);
        using var body = new SolidBrush(Color.FromArgb(206, 214, 224));
        using var edge = new Pen(Color.FromArgb(92, 106, 124), 1.2f);
        graphics.FillRectangle(body, ph);
        graphics.DrawRectangle(edge, ph.X, ph.Y, ph.Width, ph.Height);
        TextRenderer.DrawText(graphics, "ГЭС", smallFont, Rectangle.Round(ph),
            Color.FromArgb(44, 56, 72), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    // --- шкала отметок (с разведением подписей) и пунктир гребня ---
    using (var ax = new Pen(Color.FromArgb(130, 138, 148), 1f))
    using (var guide = new Pen(Color.FromArgb(150, 130, 138, 148), 1f) { DashStyle = DashStyle.Dash })
    {
        graphics.DrawLine(ax, plot.Left - 6, plot.Top, plot.Left - 6, plot.Bottom);
        var marks = new[] { bedLevel, lower, upper, crest }.Distinct().OrderByDescending(m => m).ToArray();
        var lastLabelY = float.MinValue / 2;
        foreach (var m in marks)
        {
            var ty = Y(m);
            graphics.DrawLine(ax, plot.Left - 10, ty, plot.Left - 6, ty);
            var ly = Math.Max(ty - 7, lastLabelY + 11);
            lastLabelY = ly;
            TextRenderer.DrawText(graphics, m.ToString("0.##", CultureInfo.CurrentCulture) + " м",
                smallFont, new Point(bounds.Left + 2, (int)ly), Color.FromArgb(90, 100, 112));
        }
        graphics.DrawLine(guide, plot.Left - 6, crestY, upFaceTopX - 4, crestY);
    }

    // --- размер H (высота плотины) ---
    var dimX = Math.Max(plot.Left + 2, upToeX - Math.Max(16f, upSlope + 10f));
    if (faceHeight > 14)
    {
        DrawDoubleArrow(graphics, new PointF(dimX, crestY), new PointF(dimX, bedY), dimColor);
        var hText = "H = " + (damHeight > 0 ? damHeight.ToString("0.##", CultureInfo.CurrentCulture) + " м" : "—");
        DrawProfileLabelRotated(graphics, hText, dimX - 12, (crestY + bedY) / 2f, smallFont);
    }

    // --- размер напора ΔН = НПУ − УНБ ---
    if (lowerY - upperY > 14 && plot.Right - downFaceTopX > 40)
    {
        var hx = plot.Right - 8;
        using (var lead = new Pen(Color.FromArgb(140, 70, 80, 92), 1f) { DashStyle = DashStyle.Dot })
            graphics.DrawLine(lead, downFaceTopX + 4, upperY, hx + 4, upperY);
        DrawDoubleArrow(graphics, new PointF(hx, upperY), new PointF(hx, lowerY), dimColor);
        DrawProfileLabelRotated(graphics, "ΔН = " + (upper - lower).ToString("0.##", CultureInfo.CurrentCulture) + " м",
            hx - 10, (upperY + lowerY) / 2f, smallFont);
    }

    // --- заголовок ---
    var bandRect = new RectangleF(plot.Left, bounds.Top + 8, plot.Width, 22);
    using (var band = new SolidBrush(Color.FromArgb(236, 242, 248)))
    using (var bandPen = new Pen(Color.FromArgb(196, 208, 222), 1f))
    {
        graphics.FillRectangle(band, bandRect);
        graphics.DrawRectangle(bandPen, bandRect.X, bandRect.Y, bandRect.Width, bandRect.Height);
    }
    TextRenderer.DrawText(graphics, "Схема гидроузла (продольный разрез)", titleFont,
        Rectangle.Round(bandRect), Color.FromArgb(31, 41, 55),
        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

    // --- подписи ---
    TextRenderer.DrawText(graphics, "Верхний бьеф", labelFont,
        new Point((int)plot.Left, (int)upperY - 20), Color.FromArgb(24, 74, 116));
    TextRenderer.DrawText(graphics, "Нижний бьеф", labelFont,
        new Point((int)plot.Right - 84, (int)lowerY - 20), Color.FromArgb(24, 74, 116));

    DrawProfileLabel(graphics, "НПУ / УВБ " + (hasUpper ? upper.ToString("N2", CultureInfo.CurrentCulture) : "—") + " м",
        plot.Left + 6, upperY - 16);
    DrawProfileLabel(graphics, "УНБ " + (hasLower ? lower.ToString("N2", CultureInfo.CurrentCulture) : "—") + " м",
        Math.Min(downFaceAtWater + 6, plot.Right - 90), lowerY - 16);
    DrawProfileLabel(graphics, "Гребень " + crest.ToString("N2", CultureInfo.CurrentCulture) + " м",
        damCenter - 44, Math.Max(bandRect.Bottom + 2, crestY - 30));
    DrawProfileLabel(graphics, "Дно " + bedLevel.ToString("N2", CultureInfo.CurrentCulture) + " м",
        plot.Left + 6, bedY + 4);

    // --- легенда и примечания ---
    DrawLegendChip(graphics, "Вода", waterTop, bounds.Left + 10, bounds.Bottom - 16, smallFont);
    DrawLegendChip(graphics, embankment ? "Плотина (насыпная)" : "Плотина (бетонная)",
        Color.FromArgb(150, 158, 168), bounds.Left + 72, bounds.Bottom - 16, smallFont);
    DrawLegendChip(graphics, "Грунт", Color.FromArgb(152, 118, 82), bounds.Left + 200, bounds.Bottom - 16, smallFont);

    var lenText = FormatOptional(_objectProfile.DamLengthM);
    var note = "Длина по гребню: " + (lenText == "—" ? "—" : lenText + " м") + "  |  по вертикали в масштабе, по горизонтали схематично";
    var noteSize = TextRenderer.MeasureText(graphics, note, smallFont, Size.Empty, TextFormatFlags.NoPadding);
    if (bounds.Width - 260 > noteSize.Width)
        TextRenderer.DrawText(graphics, note, smallFont,
            new Point(bounds.Right - noteSize.Width - 8, bounds.Bottom - 18),
            Color.FromArgb(90, 100, 112), TextFormatFlags.NoPadding);
}

        // Классификатор типа плотины по названию: насыпные/земляные/каменные — «да».
        private static bool IsEmbankmentDam(string? damType)
        {
            if (string.IsNullOrWhiteSpace(damType)) return false;
            var t = (damType ?? string.Empty).ToLowerInvariant();
            return t.Contains("земл") || t.Contains("камен") || t.Contains("насып") ||
                t.Contains("греб");
        }

        // Волнистая линия поверхности воды между x0 и x1 на отметке y.
        private static void DrawWaterSurface(Graphics graphics, float x0, float x1, float y, Color color, float amp)
        {
            if (x1 - x0 <= 2) return;
            using var pen = new Pen(color, 1.6f);
            const float step = 6f;
            var prev = new PointF(x0, y);
            var k = 0;
            for (var x = x0 + step; x < x1; x += step, k++)
            {
                var yy = y + ((k % 2 == 0) ? -amp : amp) * 0.6f;
                var p = new PointF(x, yy);
                graphics.DrawLine(pen, prev, p);
                prev = p;
            }
            graphics.DrawLine(pen, prev, new PointF(x1, y));
        }

        // Треугольный наконечник стрелки в точке tip, ориентированный по углу (в градусах).
        private static void DrawArrowHead(Graphics graphics, PointF tip, double angleDeg, float size, Color color)
        {
            using var brush = new SolidBrush(color);
            var angle = angleDeg * Math.PI / 180.0;
            var a1 = angle + Math.PI - 0.4;
            var a2 = angle + Math.PI + 0.4;
            var p1 = new PointF(tip.X + (float)Math.Cos(a1) * size, tip.Y + (float)Math.Sin(a1) * size);
            var p2 = new PointF(tip.X + (float)Math.Cos(a2) * size, tip.Y + (float)Math.Sin(a2) * size);
            graphics.FillPolygon(brush, new[] { tip, p1, p2 });
        }

        // Двусторонняя размерная стрелка между точками a и b.
        private static void DrawDoubleArrow(Graphics graphics, PointF a, PointF b, Color color)
        {
            using var pen = new Pen(color, 1f);
            graphics.DrawLine(pen, a, b);
            var angle = Math.Atan2(b.Y - a.Y, b.X - a.X) * 180.0 / Math.PI;
            DrawArrowHead(graphics, b, angle, 5f, color);
            DrawArrowHead(graphics, a, angle + 180, 5f, color);
        }

        // Вертикальная (повёрнутая на -90°) подпись — используется для размера высоты H.
        private void DrawProfileLabelRotated(Graphics graphics, string text, float x, float y, Font font)
        {
            if (string.IsNullOrEmpty(text)) return;
            var state = graphics.Save();
            graphics.TranslateTransform(x, y);
            graphics.RotateTransform(-90);
            var size = TextRenderer.MeasureText(graphics, text, font, Size.Empty, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(graphics, text, font, new Point(-size.Width / 2, -size.Height / 2),
                Color.FromArgb(40, 50, 62), TextFormatFlags.NoPadding);
            graphics.Restore(state);
        }

        // Элемент легенды: цветной квадратик + подпись.
        private static void DrawLegendChip(Graphics graphics, string text, Color color, float x, float y, Font font)
        {
            using var b = new SolidBrush(color);
            using var p = new Pen(Color.FromArgb(120, 128, 138), 1f);
            var r = new RectangleF(x, y - 8, 12, 10);
            graphics.FillRectangle(b, r);
            graphics.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
            TextRenderer.DrawText(graphics, text, font, new Point((int)(x + 15), (int)(y - 9)), Color.FromArgb(60, 70, 82));
        }

        // --- Профиль реального створа (из БД или из Excel-изодат глубины) ---

        // Выбирает источник данных для профиля: смоделированная модель створа,
        // импортированные изодаты глубины, либо сообщение об отсутствии данных.
        private void DrawHydraulicProfileFromDatabase(Graphics graphics, Rectangle bounds)
        {
            graphics.Clear(Color.White);
            var model = GetSelectedProfileModel();
            if (model != null && model.Points.Count > 0)
            {
                DrawDatabaseProfile(graphics, bounds, model);
                return;
            }

            if (_sectionsTree.SelectedNode?.Tag is CrossSection section &&
                (section.GetPoints(Bank.Left, IzodataType.Depth).Count > 0 ||
                 section.GetPoints(Bank.Right, IzodataType.Depth).Count > 0))
            {
                DrawImportedDepthCurves(graphics, bounds, section);
                return;
            }

            graphics.DrawString(
                "Нет данных для графика. Импортируйте Excel с изодатами глубины.",
                Font, Brushes.DimGray, 16, 16);
        }

        // Черит профиль створа из готовой HydraulicProfileRenderModel (данные БД).
        private void DrawDatabaseProfile(
            Graphics graphics,
            Rectangle bounds,
            HydraulicProfileRenderModel model)
        {
            var points = model.Points.ToList();
            var minX = Math.Min(0, points.Min(point => point.DistanceM));
            var maxX = Math.Max(1, points.Max(point => point.DistanceM));
            var minY = points.Min(point => point.ElevationM);
            var maxY = points.Max(point => point.ElevationM);
            if (Math.Abs(maxY - minY) < 0.001) { maxY += 1; minY -= 1; }
            var plot = new Rectangle(58, 24, Math.Max(40, bounds.Width - 86), Math.Max(40, bounds.Height - 62));
            Func<double, float> mapX = x => plot.Left + (float)((x - minX) / (maxX - minX) * plot.Width);
            Func<double, float> mapY = y => plot.Bottom - (float)((y - minY) / (maxY - minY) * plot.Height);

            using (var axis = new Pen(Color.FromArgb(75, 85, 99), 1))
            using (var grid = new Pen(Color.LightGray, 1))
            {
                graphics.DrawLine(axis, mapX(0), plot.Top, mapX(0), plot.Bottom);
                graphics.DrawLine(axis, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
                for (var i = 0; i <= 4; i++)
                {
                    var level = minY + (maxY - minY) * i / 4d;
                    var y = mapY(level);
                    graphics.DrawLine(grid, plot.Left, y, plot.Right, y);
                    graphics.DrawString(level.ToString("0.##"), Font, Brushes.DimGray, 4, y - 8);
                }
            }
            DrawProfileLine(graphics, model.Points, mapX, mapY);
            graphics.DrawString("Расстояние от фарватера, м", Font, Brushes.DimGray,
                Math.Max(4, plot.Left + plot.Width / 2 - 70), plot.Bottom + 24);
            graphics.DrawString("Отметка, м", Font, Brushes.DimGray, 4, 4);
        }

        // Черит кривые глубины по левому/правому берегу напрямую из импортированных Excel-изодат.
        private void DrawImportedDepthCurves(
            Graphics graphics,
            Rectangle bounds,
            CrossSection section)
        {
            var left = section.GetPoints(Bank.Left, IzodataType.Depth)
                .OrderBy(point => point.Distance).ToList();
            var right = section.GetPoints(Bank.Right, IzodataType.Depth)
                .OrderBy(point => point.Distance).ToList();
            var maxDistance = Math.Max(
                left.Count == 0 ? 0 : left.Max(point => point.Distance),
                right.Count == 0 ? 0 : right.Max(point => point.Distance));
            var maxDepth = Math.Max(
                left.Count == 0 ? 0 : left.Max(point => point.Value),
                right.Count == 0 ? 0 : right.Max(point => point.Value));
            if (maxDistance <= 0 || maxDepth <= 0)
            {
                graphics.DrawString(
                    "В Excel нет положительных расстояний и глубин для графика.",
                    Font, Brushes.DimGray, 16, 16);
                return;
            }

            var plot = new Rectangle(58, 34, Math.Max(40, bounds.Width - 86),
                Math.Max(40, bounds.Height - 72));
            float MapX(double distance) =>
                plot.Left + (float)(distance / maxDistance * plot.Width);
            float MapY(double depth) =>
                plot.Bottom - (float)(depth / maxDepth * plot.Height);

            using (var grid = new Pen(Color.FromArgb(220, 226, 232), 1))
            using (var axis = new Pen(Color.FromArgb(75, 85, 99), 1))
            {
                for (var i = 0; i <= 4; i++)
                {
                    var y = plot.Bottom - i * plot.Height / 4f;
                    graphics.DrawLine(grid, plot.Left, y, plot.Right, y);
                    graphics.DrawString(
                        (maxDepth * i / 4d).ToString("0.##", CultureInfo.CurrentCulture),
                        Font, Brushes.DimGray, 4, y - 8);
                }
                graphics.DrawLine(axis, plot.Left, plot.Top, plot.Left, plot.Bottom);
                graphics.DrawLine(axis, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            }

            DrawIsodataCurve(graphics, left, MapX, MapY, Color.FromArgb(37, 99, 235));
            DrawIsodataCurve(graphics, right, MapX, MapY, Color.FromArgb(249, 115, 22));
            graphics.DrawString(
                $"Створ №{section.Number}: глубина по изодатам Excel",
                Font, Brushes.DimGray, plot.Left, 8);
            graphics.DrawString("Расстояние, м", Font, Brushes.DimGray,
                Math.Max(4, plot.Left + plot.Width / 2 - 35), plot.Bottom + 24);
            graphics.DrawString("Глубина, м", Font, Brushes.DimGray, 4, 4);
            using var leftBrush = new SolidBrush(Color.FromArgb(37, 99, 235));
            using var rightBrush = new SolidBrush(Color.FromArgb(249, 115, 22));
            graphics.FillRectangle(leftBrush, plot.Right - 145, 8, 10, 10);
            graphics.DrawString("Левый берег", Font, Brushes.DimGray, plot.Right - 131, 5);
            graphics.FillRectangle(rightBrush, plot.Right - 65, 8, 10, 10);
            graphics.DrawString("Правый", Font, Brushes.DimGray, plot.Right - 51, 5);
        }

        // Линия + маркеры одной изодаты (глубины) с произвольной проекцией координат.
        private static void DrawIsodataCurve(
            Graphics graphics,
            IReadOnlyList<IzodataPoint> points,
            Func<double, float> mapX,
            Func<double, float> mapY,
            Color color)
        {
            if (points.Count == 0)
                return;

            using var pen = new Pen(color, 2);
            using var brush = new SolidBrush(color);
            var mapped = points
                .Select(point => new PointF(mapX(point.Distance), mapY(point.Value)))
                .ToArray();
            if (mapped.Length > 1)
                graphics.DrawLines(pen, mapped);
            foreach (var point in mapped)
                graphics.FillEllipse(brush, point.X - 3, point.Y - 3, 6, 6);
        }

        // Ломаная профиля по точкам ProfilePoint с подписями номеров точек.
        private static void DrawProfileLine(Graphics graphics, IReadOnlyList<ProfilePoint> points,
            Func<double, float> mapX, Func<double, float> mapY)
        {
            if (points.Count == 0) return;
            using var line = new Pen(Color.FromArgb(30, 105, 160), 2);
            using var pointBrush = new SolidBrush(Color.FromArgb(30, 105, 160));
            for (var i = 1; i < points.Count; i++)
                graphics.DrawLine(line, mapX(points[i - 1].DistanceM), mapY(points[i - 1].ElevationM),
                    mapX(points[i].DistanceM), mapY(points[i].ElevationM));
            foreach (var point in points)
            {
                var x = mapX(point.DistanceM);
                var y = mapY(point.ElevationM);
                graphics.FillEllipse(pointBrush, x - 3, y - 3, 6, 6);
                graphics.DrawString(point.PointNumber.ToString(CultureInfo.InvariantCulture),
                    SystemFonts.DefaultFont, Brushes.Black, x + 4, y - 8);
            }
        }

        // Преобразует абсолютную отметку в экранный Y в заданном прямоугольнике графика.
        private static int MapLevel(double level, double bottom, double upper, Rectangle plot)
        {
            var fraction = (level - bottom) / Math.Max(0.01, upper - bottom);
            fraction = Math.Max(0, Math.Min(1, fraction));
            return plot.Bottom - (int)(fraction * plot.Height);
        }

        // Подпись с полупрозрачной белой плашкой — читается поверх воды/грунта.
        private void DrawProfileLabel(Graphics graphics, string text, float x, float y)
        {
            if (string.IsNullOrEmpty(text)) return;
            var size = TextRenderer.MeasureText(graphics, text, Font, Size.Empty, TextFormatFlags.NoPadding);
            var rect = new Rectangle((int)x, (int)y, size.Width + 8, size.Height + 3);
            using (var bg = new SolidBrush(Color.FromArgb(210, 255, 255, 255)))
            using (var br = new Pen(Color.FromArgb(150, 198, 206, 214), 0.8f))
            {
                graphics.FillRectangle(bg, rect);
                graphics.DrawRectangle(br, rect.X, rect.Y, rect.Width, rect.Height);
            }
            TextRenderer.DrawText(graphics, text, Font, new Point((int)x + 4, (int)y + 1),
                Color.FromArgb(20, 28, 38), TextFormatFlags.NoPadding);
        }

        // ================= ПОПЕРЕЧНЫЙ РАЗРЕЗ СТВОРА (по выбранному типу изодаты) =================
        //
        // Строится по импортированным из Excel изодатам выбранного створа. Берега зеркалятся
        // относительно фарватера: левый уходит влево (отрицательное расстояние), правый — вправо.
        // Тип изодаты переключается комбобоксом на вкладке «Cross sections and bank points».

        // Индекс комбобокса → тип изодаты.
        private static IzodataType IzodataTypeForIndex(int index) => index switch
        {
            1 => IzodataType.Velocity,
            2 => IzodataType.DryingCalculated,
            3 => IzodataType.DryingFactual,
            _ => IzodataType.Depth
        };

        // Точка входа Paint: определяет выбранный створ и рисует его разрез.
        private void DrawSelectedCrossSection(Graphics graphics, Rectangle bounds)
        {
            graphics.Clear(Color.White);
            if (bounds.Width < 160 || bounds.Height < 120)
                return;

            var section = _sectionsTree?.SelectedNode?.Tag as CrossSection;
            if (section == null && _sectionsTree?.SelectedNode?.Tag is CrossSectionRecord rec)
                section = _sections.FirstOrDefault(s => s.Number == rec.Number);

            if (section == null)
            {
                TextRenderer.DrawText(graphics,
                    "Выберите створ, импортированный из Excel.",
                    Font, new Rectangle(bounds.Left + 12, bounds.Top + 10, bounds.Width - 24, 40),
                    Color.DimGray);
                return;
            }

            DrawCrossSectionProfile(graphics, bounds, section, _selectedIzodataType);
        }

        // Собственно отрисовка зеркального поперечного разреза для одного типа изодаты.
        private void DrawCrossSectionProfile(
            Graphics graphics, Rectangle bounds, CrossSection section, IzodataType type)
        {
            var oldSmooth = graphics.SmoothingMode;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                var left = section.GetPoints(Bank.Left, type).OrderBy(p => p.Distance).ToList();
                var right = section.GetPoints(Bank.Right, type).OrderBy(p => p.Distance).ToList();
                if (left.Count == 0 && right.Count == 0)
                {
                    TextRenderer.DrawText(graphics,
                        $"Створ №{section.Number}: нет точек типа «{GetIzodataTypeName(type)}».",
                        Font, new Rectangle(bounds.Left + 12, bounds.Top + 10, bounds.Width - 24, 40),
                        Color.DimGray);
                    return;
                }

                // Левый берег зеркалим влево, правый — вправо; сортировка по X даёт непрерывный профиль.
                var pts = new List<CrossPoint>();
                for (var i = 0; i < left.Count; i++)
                    pts.Add(new CrossPoint(-left[i].Distance, left[i].Value, "\u041b-" + (i + 1)));
                for (var i = 0; i < right.Count; i++)
                    pts.Add(new CrossPoint(right[i].Distance, right[i].Value, "\u041f-" + (i + 1)));
                pts = pts.OrderBy(q => q.X).ToList();

                var minX = pts.Min(q => q.X);
                var maxX = pts.Max(q => q.X);
                if (maxX - minX < 0.001) { minX -= 1; maxX += 1; }
                var minV = Math.Min(0, pts.Min(q => q.Y));
                var maxV = pts.Max(q => q.Y);
                if (maxV - minV < 0.001) maxV = minV + 1;

                var plot = new Rectangle(56, 40, Math.Max(40, bounds.Width - 74),
                    Math.Max(40, bounds.Height - 88));
                float MapX(double x) => plot.Left + (float)((x - minX) / (maxX - minX) * plot.Width);
                var isDepth = type == IzodataType.Depth;
                // Глубина: 0 сверху и растёт вниз (русло-«корыто»). Прочие: значение растёт вверх.
                float MapY(double v) => isDepth
                    ? plot.Top + (float)((v - minV) / (maxV - minV) * plot.Height)
                    : plot.Bottom - (float)((v - minV) / (maxV - minV) * plot.Height);

                var lineColor = type switch
                {
                    IzodataType.Depth => Color.FromArgb(30, 105, 160),
                    IzodataType.Velocity => Color.FromArgb(16, 150, 133),
                    IzodataType.DryingCalculated => Color.FromArgb(217, 119, 6),
                    _ => Color.FromArgb(220, 38, 38)
                };
                string Unit() => type == IzodataType.Velocity ? "\u043c/\u0441" : "\u043c";
                var bed = pts.Select(q => new PointF(MapX(q.X), MapY(q.Y))).ToArray();

                if (isDepth)
                {
                    // Реалистичный разрез русла: водная толща + штриховка грунта.
                    var sy = MapY(minV);
                    var water = new List<PointF> { new PointF(bed[0].X, sy) };
                    water.AddRange(bed);
                    water.Add(new PointF(bed[bed.Length - 1].X, sy));
                    WaterFill(graphics, water.ToArray(), plot);
                    var earth = new List<PointF>(bed)
                    {
                        new PointF(bed[bed.Length - 1].X, plot.Bottom),
                        new PointF(bed[0].X, plot.Bottom),
                    };
                    HatchPolygon(graphics, earth.ToArray(),
                        Color.FromArgb(202, 174, 134), Color.FromArgb(122, 94, 60));
                    using (var bedPen = new Pen(Color.FromArgb(92, 68, 44), 2f))
                        graphics.DrawLines(bedPen, bed);
                    DrawWaterSurface(graphics, plot.Left, plot.Right, sy);
                }
                else
                {
                    var fillPoly = new List<PointF> { new PointF(MapX(minX), plot.Bottom) };
                    fillPoly.AddRange(bed);
                    fillPoly.Add(new PointF(MapX(maxX), plot.Bottom));
                    AreaFill(graphics, fillPoly.ToArray(), plot, lineColor);
                    using var line = new Pen(lineColor, 2);
                    graphics.DrawLines(line, bed);
                }

                // Горизонтальная сетка с подписями значений.
                using (var gridPen = new Pen(Color.FromArgb(isDepth ? 90 : 226, 226, 232, 238), 1))
                {
                    for (var i = 0; i <= 4; i++)
                    {
                        var val = minV + (maxV - minV) * i / 4d;
                        var y = MapY(val);
                        graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                        TextRenderer.DrawText(graphics, val.ToString("0.##", CultureInfo.CurrentCulture),
                            SystemFonts.DefaultFont, new Point(4, (int)y - 8), Color.DimGray);
                    }
                }

                // Пунктирная ось фарватера (x = 0), если он внутри диапазона расстояний.
                if (minX < 0 && maxX > 0)
                {
                    using var axisPen = new Pen(Color.FromArgb(120, 130, 140), 1) { DashStyle = DashStyle.Dash };
                    var cx = MapX(0);
                    graphics.DrawLine(axisPen, cx, plot.Top, cx, plot.Bottom);
                    TextRenderer.DrawText(graphics, "\u0444\u0430\u0440\u0432\u0430\u0442\u0435\u0440", SystemFonts.DefaultFont,
                        new Point((int)cx - 22, plot.Bottom + 2), Color.Gray);
                }

                // Маркеры исходных точек с метками берега.
                using (var marker = new SolidBrush(isDepth ? Color.FromArgb(18, 62, 98) : lineColor))
                {
                    for (var i = 0; i < bed.Length; i++)
                    {
                        var sp = bed[i];
                        graphics.FillEllipse(marker, sp.X - 3, sp.Y - 3, 6, 6);
                        if (bed.Length <= 40)
                            TextRenderer.DrawText(graphics, pts[i].Label, SystemFonts.DefaultFont,
                                new Point((int)sp.X + 5, (int)sp.Y - 6), Color.FromArgb(40, 50, 62));
                    }
                }

                // Размерные линии / выноски значений.
                var extremeIndex = 0;
                for (var i = 1; i < pts.Count; i++)
                    if (pts[i].Y > pts[extremeIndex].Y) extremeIndex = i;
                var ex = pts[extremeIndex];
                if (isDepth)
                {
                    var sy = MapY(minV);
                    DimH(graphics, MapX(minX), MapX(maxX), plot.Bottom + 30,
                        $"B \u2248 {(maxX - minX):0.#} \u043c", Color.FromArgb(70, 80, 92));
                    DimV(graphics, MapX(ex.X), sy, MapY(ex.Y),
                        $"Hmax \u2248 {ex.Y:0.#} \u043c", Color.FromArgb(18, 62, 98));
                    TextRenderer.DrawText(graphics,
                        "\u043f\u043e\u0432\u0435\u0440\u0445\u043d\u043e\u0441\u0442\u044c \u0432\u043e\u0434\u044b",
                        SystemFonts.DefaultFont, new Point(plot.Right - 108, (int)sy - 14), Color.FromArgb(24, 74, 116));
                }
                else
                {
                    TextRenderer.DrawText(graphics,
                        $"\u043c\u0430\u043a\u0441.: {ex.Y:0.##} {Unit()}", Font,
                        new Point((int)MapX(ex.X) + 8, (int)MapY(ex.Y) - 18), lineColor);
                }

                using (var border = new Pen(Color.FromArgb(150, 158, 168), 1))
                    graphics.DrawRectangle(border, plot.Left, plot.Top, plot.Width, plot.Height);

                var unit = type == IzodataType.Velocity ? "м/с" : "м";
                TextRenderer.DrawText(graphics,
                    $"Створ №{section.Number} — {GetIzodataTypeName(type)} ({unit})   " +
                    $"Левый: {left.Count} · Правый: {right.Count} · Всего: {pts.Count}",
                    Font, new Point(plot.Left, bounds.Top + 8), Color.FromArgb(31, 41, 55));
                TextRenderer.DrawText(graphics, "\u2190 левый берег", SystemFonts.DefaultFont,
                    new Point(plot.Left + 2, plot.Bottom + 16), Color.FromArgb(37, 99, 235));
                TextRenderer.DrawText(graphics, "правый берег \u2192", SystemFonts.DefaultFont,
                    new Point(Math.Max(plot.Left + 2, plot.Right - 100), plot.Bottom + 16), Color.FromArgb(249, 115, 22));
            }
            finally
            {
                graphics.SmoothingMode = oldSmooth;
            }
        }

        // Точка разреза: X — знаковое расстояние от фарватера, Y — значение изодаты, Label — метка берега.
        private readonly struct CrossPoint
        {
            public CrossPoint(double x, double y, string label) { X = x; Y = y; Label = label; }
            public double X { get; }
            public double Y { get; }
            public string Label { get; }
        }
    }
}
