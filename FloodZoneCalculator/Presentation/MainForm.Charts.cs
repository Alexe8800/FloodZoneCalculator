#nullable enable

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation
{
    // =====================================================================================
    // MainForm.Charts.cs — отрисовка аналитических графиков на вкладке результатов.
    //   DrawBalanceChart     — круговая диаграмма баланса водопользования.
    //   DrawFlowChart        — кривая обеспеченности расходов (линейная/логарифмическая).
    //   DrawGenerationChart  — столбчатый график месячной выработки электроэнергии.
    //   Плюс общие примитивы: прямоугольник области построения, оси, чтение точек из грида.
    // =====================================================================================
    public sealed partial class CalculationForm
    {
        // Круговая диаграмма долей водопользования по строкам _balanceGrid.
        private void DrawBalanceChart(Graphics graphics, Rectangle bounds)
        {
            graphics.Clear(Color.White);
            var values = new List<double>();
            foreach (DataGridViewRow row in _balanceGrid.Rows)
            {
                var text = Convert.ToString(row.Cells[1].Value) ?? "";
                values.Add(TryParseDecimal(text, out var value) && value >= 0 ? (double)value : 0);
            }

            var total = values.Sum();
            int diameter;
            if (total <= 0)
            {
                using var empty = new SolidBrush(Color.LightGray);
                diameter = Math.Max(50, Math.Min(bounds.Height - 24, bounds.Width / 2));
                var pieEmpty = new Rectangle(10, Math.Max(8, (bounds.Height - diameter) / 2), diameter, diameter);
                graphics.FillEllipse(empty, pieEmpty);
                TextRenderer.DrawText(graphics, "Введите доли", Font,
                    new Point(pieEmpty.Left, pieEmpty.Bottom + 2), Color.Gray);
                return;
            }

            var colors = new[] { Color.FromArgb(37, 99, 235), Color.FromArgb(249, 115, 22), Color.FromArgb(34, 197, 94), Color.FromArgb(156, 163, 175) };
            diameter = Math.Max(50, Math.Min(bounds.Height - 24, bounds.Width / 2));
            var pie = new Rectangle(10, Math.Max(8, (bounds.Height - diameter) / 2), diameter, diameter);
            var start = -90f;
            for (var i = 0; i < values.Count; i++)
            {
                var sweep = (float)(values[i] / total * 360);
                using var brush = new SolidBrush(colors[i % colors.Length]);
                graphics.FillPie(brush, pie, start, sweep);
                start += sweep;
            }

            var legendX = pie.Right + 16;
            var legendWidth = Math.Max(10, bounds.Right - legendX - 8);
            for (var i = 0; i < _balanceGrid.Rows.Count; i++)
            {
                var label = Convert.ToString(_balanceGrid.Rows[i].Cells[0].Value) ?? "";
                var percent = values[i] / total * 100;
                var y = Math.Max(8, (bounds.Height - _balanceGrid.Rows.Count * 28) / 2) + i * 28;
                using var brush = new SolidBrush(colors[i % colors.Length]);
                graphics.FillRectangle(brush, legendX, y + 3, 12, 12);
                TextRenderer.DrawText(graphics, label + " " + percent.ToString("0.##") + "%",
                    Font, new Rectangle(legendX + 18, y, legendWidth - 18, 25),
                    Color.FromArgb(31, 41, 55), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        // Кривая «расход — обеспеченность». logarithmic=true строит шкалу по log10 обеспеченности.
        private void DrawFlowChart(Graphics graphics, Rectangle bounds, bool logarithmic)
        {
            graphics.Clear(Color.White);
            var points = ReadFlowPoints();
            DrawAxes(graphics, bounds, "Расход, м³/с", logarithmic ? "Обеспеченность, % (лог.)" : "Обеспеченность, %");
            if (points.Count < 2) return;

            var plot = GetPlotRectangle(bounds);
            var minX = logarithmic ? Math.Max(0.01, points.Min(p => p.P)) : 0;
            var maxX = 100d;
            var maxY = Math.Max(1, points.Max(p => p.Q));
            PointF Map(FlowPoint p)
            {
                var x = logarithmic
                    ? (Math.Log10(Math.Max(minX, p.P)) - Math.Log10(minX)) /
                      (Math.Log10(maxX) - Math.Log10(minX))
                    : p.P / maxX;
                return new PointF((float)(plot.Left + x * plot.Width),
                    (float)(plot.Bottom - p.Q / maxY * plot.Height));
            }

            using var pen = new Pen(Color.FromArgb(37, 99, 235), 2);
            var mapped = points.Select(Map).ToArray();
            graphics.DrawLines(pen, mapped);
            using var brush = new SolidBrush(Color.FromArgb(37, 99, 235));
            foreach (var point in mapped)
                graphics.FillEllipse(brush, point.X - 3, point.Y - 3, 6, 6);
        }

        // Столбчатый график выработки по 12 месяцам из колонки «Generation» грида.
        private void DrawGenerationChart(Graphics graphics, Rectangle bounds)
        {
            graphics.Clear(Color.White);
            var plot = GetPlotRectangle(bounds, 28, 12, 24, 34);
            using var gridPen = new Pen(Color.FromArgb(229, 231, 235));
            for (var i = 0; i <= 4; i++)
            {
                var y = plot.Bottom - i * plot.Height / 4f;
                graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            }
            var values = _generationGrid.Rows.Cast<DataGridViewRow>()
                .Select(row => ReadCellNumber(row.Cells[3].Value)).ToArray();
            if (values.All(value => value <= 0))
            {
                TextRenderer.DrawText(
                    graphics,
                    "Заполните месячные значения во вкладке «Табличные данные».",
                    Font,
                    new Rectangle(plot.Left + 8, plot.Top + 12, plot.Width - 16, 42),
                    Color.DimGray);
                return;
            }
            var max = Math.Max(1, values.Max());
            var width = plot.Width / Math.Max(1, values.Length);
            using var brush = new SolidBrush(Color.FromArgb(37, 99, 235));
            using var axisPen = new Pen(Color.FromArgb(107, 114, 128));
            graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
            graphics.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            for (var i = 0; i < values.Length; i++)
            {
                var gridY = plot.Bottom - i * plot.Height / 4f;
                graphics.DrawString((max * i / 4d).ToString("0.#", CultureInfo.CurrentCulture),
                    Font, Brushes.DimGray, 2, gridY - 8);
                var height = values[i] / max * plot.Height;
                var bar = new RectangleF(plot.Left + i * width + width * .18f,
                    plot.Bottom - (float)height, width * .64f, (float)height);
                graphics.FillRectangle(brush, bar);
                TextRenderer.DrawText(graphics, Convert.ToString(_generationGrid.Rows[i].Cells[0].Value) ?? "",
                    Font, new Rectangle((int)(plot.Left + i * width), plot.Bottom + 2,
                        Math.Max(1, (int)width), 18), Color.FromArgb(55, 65, 81),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
            }
            TextRenderer.DrawText(graphics, "Месяц", Font, new Point(plot.Left + plot.Width / 2 - 20, plot.Bottom + 19),
                Color.FromArgb(31, 41, 55));
        }

        // Считывает пары (обеспеченность P, расход Q) из грида изодат, отбрасывая пустые строки.
        private List<FlowPoint> ReadFlowPoints()
        {
            return _flowProvisionGrid.Rows.Cast<DataGridViewRow>()
                .Select(row => new FlowPoint(ReadCellNumber(row.Cells[0].Value), ReadCellNumber(row.Cells[1].Value)))
                .Where(point => point.P > 0 && point.Q >= 0)
                .OrderBy(point => point.P)
                .ToList();
        }

        // Прямоугольник области построения графика внутри bounds с полями под оси/подписи.
        private static Rectangle GetPlotRectangle(Rectangle bounds, int left = 48, int top = 22, int right = 18, int bottom = 34) =>
            new Rectangle(bounds.Left + left, bounds.Top + top,
                Math.Max(10, bounds.Width - left - right), Math.Max(10, bounds.Height - top - bottom));

        // Рисует сетку по горизонтали и оси координат с заголовками.
        private void DrawAxes(Graphics graphics, Rectangle bounds, string yTitle, string xTitle)
        {
            var plot = GetPlotRectangle(bounds);
            using var gridPen = new Pen(Color.FromArgb(229, 231, 235));
            using var axisPen = new Pen(Color.FromArgb(107, 114, 128));
            for (var i = 0; i <= 4; i++)
            {
                var y = plot.Bottom - i * plot.Height / 4f;
                graphics.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            }
            graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
            graphics.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            TextRenderer.DrawText(graphics, yTitle, Font, new Point(4, 4), Color.Black);
            TextRenderer.DrawText(graphics, xTitle, Font, new Point(plot.Left + 10, plot.Bottom + 10), Color.Black);
        }

        // Точка кривой обеспеченности: P — вероятность (%), Q — расход (м³/с).
        private readonly struct FlowPoint
        {
            public FlowPoint(double p, double q) { P = p; Q = q; }
            public double P { get; }
            public double Q { get; }
        }
    }
}
