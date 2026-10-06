#nullable enable
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation.Educational;

public sealed class EducationalProfileChartControl : UserControl
{
    private EducationalProfileRenderFrame? _frame;

    public EducationalProfileChartControl()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        MinimumSize = new Size(420, 280);
    }

    public EducationalProfileRenderFrame? Frame
    {
        get => _frame;
        set
        {
            _frame = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_frame == null || _frame.Points.Count == 0)
            return;

        var plot = new Rectangle(64, 20, Math.Max(1, ClientSize.Width - 90),
            Math.Max(1, ClientSize.Height - 70));
        var xMin = _frame.Points.Min(point => point.X);
        var xMax = _frame.Points.Max(point => point.X);
        var yMin = Math.Min(_frame.Points.Min(point => point.Y),
            _frame.ReferenceDepth ?? _frame.Points.Min(point => point.Y));
        var yMax = Math.Max(_frame.Points.Max(point => point.Y),
            _frame.ReferenceDepth ?? _frame.Points.Max(point => point.Y));
        var xRange = xMax - xMin;
        var yRange = yMax - yMin;
        if (xRange == 0) xRange = 1;
        if (yRange == 0) yRange = 1;

        PointF Map(EducationalPlotPoint point) =>
            new(
                plot.Left + (float)((point.X - xMin) / xRange * plot.Width),
                plot.Bottom - (float)((point.Y - yMin) / yRange * plot.Height));

        using (var axisPen = new Pen(Color.Gray))
        {
            e.Graphics.DrawLine(axisPen, plot.Left, plot.Top, plot.Left, plot.Bottom);
            e.Graphics.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
        }

        using (var profilePen = new Pen(Color.DarkBlue, 2))
        {
            foreach (var segment in _frame.Segments)
                e.Graphics.DrawLine(profilePen, Map(segment.Start), Map(segment.End));
        }

        if (_frame.ReferenceDepth.HasValue)
        {
            var y = plot.Bottom - (float)((_frame.ReferenceDepth.Value - yMin) / yRange * plot.Height);
            using (var referencePen = new Pen(Color.DodgerBlue, 1))
                e.Graphics.DrawLine(referencePen, plot.Left, y, plot.Right, y);
            e.Graphics.DrawString(_frame.ReferenceLabel ?? "", Font, Brushes.DodgerBlue,
                plot.Left + 4, Math.Max(plot.Top, y - Font.Height - 2));
        }

        using (var pointBrush = new SolidBrush(Color.DarkRed))
        {
            foreach (var point in _frame.Points)
            {
                var mapped = Map(point);
                e.Graphics.FillEllipse(pointBrush, mapped.X - 4, mapped.Y - 4, 8, 8);
            }
        }

        e.Graphics.DrawString(_frame.XAxisTitle, Font, Brushes.Black,
            plot.Left + Math.Max(0, (plot.Width - TextRenderer.MeasureText(_frame.XAxisTitle, Font).Width) / 2),
            plot.Bottom + 30);
        e.Graphics.DrawString(_frame.YAxisTitle, Font, Brushes.Black, 2, plot.Top);
        e.Graphics.DrawString(xMin.ToString("G6", System.Globalization.CultureInfo.InvariantCulture),
            Font, Brushes.Gray, plot.Left, plot.Bottom + 2);
        e.Graphics.DrawString(xMax.ToString("G6", System.Globalization.CultureInfo.InvariantCulture),
            Font, Brushes.Gray, plot.Right - 48, plot.Bottom + 2);
    }
}
