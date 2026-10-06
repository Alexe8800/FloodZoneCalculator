#nullable enable
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation.Visualization;

public sealed class CrossSectionProfileControl : UserControl
{
    private readonly CrossSectionProfileRenderer _renderer = new();
    private CrossSectionVisualizationModel? _model;
    private CrossSectionProfileRenderFrame? _frame;
    private Label _details = null!;
    private Panel _plot = null!;

    public CrossSectionProfileControl()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        MinimumSize = new Size(420, 280);
        BuildLayout();
    }

    public CrossSectionVisualizationModel? Model
    {
        get => _model;
        set
        {
            _model = value;
            _frame = value == null ? null : _renderer.Prepare(value);
            UpdateDetails();
            Invalidate();
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
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));

        _plot = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };
        _plot.Paint += PaintProfile;

        _details = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8),
            AutoEllipsis = false,
            Font = new Font("Segoe UI", 9F)
        };
        layout.Controls.Add(_plot, 0, 0);
        layout.Controls.Add(_details, 0, 1);
        Controls.Add(layout);
    }

    private void UpdateDetails()
    {
        if (_model == null)
        {
            _details.Text = "Поперечный створ не выбран.";
            return;
        }

        _details.Text = string.Join(
            Environment.NewLine,
            "Створ №" + _model.CrossSectionNumber,
            "DistanceFromHydroUnitM = " + Format(_model.DistanceFromHydroUnitM),
            "WaterLevel = " + Format(_model.WaterLevel),
            "Area = " + Format(_model.Area),
            "GeometricPerimeterBelowWaterLevel = " +
                Format(_model.GeometricPerimeterBelowWaterLevel),
            "Omega = Area (synthetic test model): " + Format(_model.Omega),
            "Chi = GeometricPerimeterBelowWaterLevel (synthetic test model): " +
                Format(_model.Chi),
            "HydraulicRadius = " + Format(_model.HydraulicRadius),
            "HydraulicSlope = " + Format(_model.HydraulicSlope),
            "ShearVelocity = " + Format(_model.ShearVelocity),
            "ChezyCoefficient = " + Format(_model.ChezyCoefficient),
            "Velocity = " + Format(_model.Velocity),
            "Discharge = " + Format(_model.Discharge));
    }

    private void PaintProfile(object? sender, PaintEventArgs e)
    {
        if (_model == null || _frame == null || _model.ProfilePoints.Count == 0)
            return;

        var plot = new Rectangle(52, 16, Math.Max(1, _plot.Width - 72),
            Math.Max(1, _plot.Height - 48));
        var xMin = _model.ProfilePoints.Min(point => point.X);
        var xMax = _model.ProfilePoints.Max(point => point.X);
        var zMin = Math.Min(_model.ProfilePoints.Min(point => point.Z), _model.WaterLevel);
        var zMax = Math.Max(_model.ProfilePoints.Max(point => point.Z), _model.WaterLevel);
        var xRange = xMax - xMin;
        var zRange = zMax - zMin;
        if (xRange == 0) xRange = 1;
        if (zRange == 0) zRange = 1;

        PointF Map(VisualizationRenderPoint point) =>
            new(
                plot.Left + (float)((point.X - xMin) / xRange * plot.Width),
                plot.Bottom - (float)((point.Z - zMin) / zRange * plot.Height));

        using var wetBrush = new SolidBrush(Color.FromArgb(80, Color.SteelBlue));
        foreach (var segment in _frame.WetAreaPolygons)
        {
            var start = Map(segment.Start);
            var end = Map(segment.End);
            var waterStart = Map(new VisualizationRenderPoint(
                segment.Start.X, _model.WaterLevel));
            var waterEnd = Map(new VisualizationRenderPoint(
                segment.End.X, _model.WaterLevel));
            e.Graphics.FillPolygon(
                wetBrush,
                new[] { start, end, waterEnd, waterStart });
        }

        using var profilePen = new Pen(Color.DarkBlue, 2);
        foreach (var segment in _frame.ProfileSegments)
            e.Graphics.DrawLine(profilePen, Map(segment.Start), Map(segment.End));

        using var waterPen = new Pen(Color.DodgerBlue, 1);
        var waterLeft = Map(new VisualizationRenderPoint(xMin, _model.WaterLevel));
        var waterRight = Map(new VisualizationRenderPoint(xMax, _model.WaterLevel));
        e.Graphics.DrawLine(waterPen, waterLeft, waterRight);

        using var pointBrush = new SolidBrush(Color.DarkRed);
        foreach (var point in _model.ProfilePoints)
        {
            var mapped = Map(new VisualizationRenderPoint(point.X, point.Z));
            e.Graphics.FillEllipse(pointBrush, mapped.X - 3, mapped.Y - 3, 6, 6);
        }

        e.Graphics.DrawString(
            "DistanceM",
            Font,
            Brushes.Black,
            plot.Left + plot.Width / 2 - 28,
            plot.Bottom + 24);
        e.Graphics.DrawString(
            "ElevationM",
            Font,
            Brushes.Black,
            2,
            2);
        e.Graphics.DrawString(
            Format(_model.WaterLevel),
            Font,
            Brushes.DodgerBlue,
            plot.Left,
            waterLeft.Y - Font.Height - 2);
    }

    private static string Format(double value) =>
        value.ToString("G6", CultureInfo.InvariantCulture);
}
