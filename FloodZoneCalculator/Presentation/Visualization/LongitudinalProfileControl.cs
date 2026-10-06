#nullable enable
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation.Visualization;

public sealed class LongitudinalProfileControl : UserControl
{
    private readonly LongitudinalProfileRenderer _renderer = new();
    private HydraulicVisualizationModel? _model;
    private LongitudinalProfileRenderFrame? _frame;
    private Panel _plot = null!;
    private DataGridView _segments = null!;

    public LongitudinalProfileControl()
    {
        BackColor = Color.White;
        MinimumSize = new Size(500, 320);
        BuildLayout();
    }

    public HydraulicVisualizationModel? Model
    {
        get => _model;
        set
        {
            _model = value;
            _frame = value == null ? null : _renderer.Prepare(value);
            UpdateSegments();
            _plot.Invalidate();
        }
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.White
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));

        _plot = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        _plot.Paint += PaintProfile;
        _segments = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        _segments.Columns.Add("FirstNumber", "First Number");
        _segments.Columns.Add("SecondNumber", "Second Number");
        _segments.Columns.Add("DeltaDistanceM", "DeltaDistanceM");
        _segments.Columns.Add("BottomLevelDifferenceM", "BottomLevelDifferenceM");
        _segments.Columns.Add("NeutralGeometryRatio", "Neutral geometry ratio");

        layout.Controls.Add(_plot, 0, 0);
        layout.Controls.Add(_segments, 0, 1);
        Controls.Add(layout);
    }

    private void UpdateSegments()
    {
        _segments.Rows.Clear();
        if (_frame == null)
            return;

        foreach (var segment in _frame.Segments)
        {
            _segments.Rows.Add(
                segment.Start.Number,
                segment.End.Number,
                Format(segment.DeltaDistanceM),
                Format(segment.BottomLevelDifferenceM),
                Format(segment.NeutralGeometryRatio));
        }
    }

    private void PaintProfile(object? sender, PaintEventArgs e)
    {
        e.Graphics.Clear(Color.White);
        if (_frame == null || _frame.Points.Count == 0 ||
            _plot.Width < 100 || _plot.Height < 100)
            return;

        var plot = new Rectangle(58, 18, _plot.Width - 82, _plot.Height - 62);
        var xMin = _frame.Points.Min(point => point.X);
        var xMax = _frame.Points.Max(point => point.X);
        var yMin = _frame.Points.Min(point => point.Y);
        var yMax = _frame.Points.Max(point => point.Y);
        var xRange = xMax - xMin;
        var yRange = yMax - yMin;
        if (xRange == 0) xRange = 1;
        if (yRange == 0) yRange = 1;

        PointF Map(LongitudinalRenderPoint point) =>
            new(
                plot.Left + (float)((point.X - xMin) / xRange * plot.Width),
                plot.Bottom - (float)((point.Y - yMin) / yRange * plot.Height));

        using var linePen = new Pen(Color.SteelBlue, 2);
        foreach (var segment in _frame.Segments)
            e.Graphics.DrawLine(linePen, Map(segment.Start), Map(segment.End));

        using var pointBrush = new SolidBrush(Color.DarkRed);
        using var numberFont = new Font(Font, FontStyle.Bold);
        foreach (var point in _frame.Points)
        {
            var location = Map(point);
            e.Graphics.FillEllipse(pointBrush, location.X - 4, location.Y - 4, 8, 8);
            e.Graphics.DrawString(
                point.Number.ToString(CultureInfo.InvariantCulture),
                numberFont,
                Brushes.Black,
                location.X + 5,
                location.Y - numberFont.Height);
        }

        e.Graphics.DrawString(
            "DistanceFromHydroUnitM",
            Font,
            Brushes.Black,
            plot.Left + plot.Width / 2 - 65,
            plot.Bottom + 28);
        e.Graphics.DrawString("BottomLevelZb", Font, Brushes.Black, 2, 2);
    }

    private static string Format(double value) =>
        value.ToString("G6", CultureInfo.InvariantCulture);
}
