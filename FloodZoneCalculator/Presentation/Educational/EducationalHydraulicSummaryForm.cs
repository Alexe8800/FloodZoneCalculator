#nullable enable
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using FloodZoneDb.Client;

namespace FloodZoneCalculator.Presentation.Educational;

public sealed class EducationalHydraulicSummaryForm : Form
{
    private readonly EducationalHydraulicSummaryModel _model;
    private EducationalHydraulicSummarySelectionState _selection;
    private readonly EducationalHydraulicSummaryRenderModels _renderModels;
    private readonly EducationalHydraulicReport _report = new();
    private readonly DataGridView _results = new();
    private readonly FlowLayoutPanel _details = new();

    public EducationalHydraulicSummaryForm(
        System.Collections.Generic.IEnumerable<EducationalHydraulicProfileData> profiles)
    {
        _model = new EducationalHydraulicSummaryModel(profiles);
        _selection = new EducationalHydraulicSummarySelectionState(_model);
        _renderModels = new EducationalHydraulicSummaryRenderModels(_model);

        Text = "Сводка учебного гидравлического расчёта";
        Width = 1500;
        Height = 980;
        MinimumSize = new Size(1150, 760);
        StartPosition = FormStartPosition.CenterParent;
        BuildLayout();
        PopulateResults();
        SelectRow(1, IsodatSide.Left);
    }

    public EducationalHydraulicSummaryModel SummaryModel => _model;
    public EducationalHydraulicProfileData SelectedProfile =>
        _selection.SelectedProfile;

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        var banner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.FromArgb(255, 243, 205),
            Padding = new Padding(8, 4, 8, 4)
        };
        banner.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        banner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        banner.Controls.Add(new Label
        {
            Text = "УЧЕБНЫЙ СИНТЕТИЧЕСКИЙ РАСЧЁТ",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.DarkRed,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        banner.Controls.Add(new Label
        {
            Text = "Результаты получены с использованием специально введённых учебных правил и параметров. Они не являются восстановлением официальной методики.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);

        var parameters = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 4, 8, 4),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };
        var first = _model.Profiles[0].HydraulicResult;
        var firstProfile = _model.Profiles[0];
        parameters.Text =
            $"Исходные данные: Depth — {firstProfile.DepthPoints.Count} точек на профиль; "
            + $"Velocity — {firstProfile.VelocityPoints.Count} точек на профиль; "
            + $"EducationalWaterDepth = {Format(first.EducationalWaterDepth)} м.  "
            + $"Синтетические параметры: g = {Format(first.Gravity)}; "
            + $"nu = {Format(first.KinematicViscosity)}; "
            + $"h = {Format(first.EducationalWaterDepth)}; "
            + $"If = {Format(first.HydraulicSlope)}.";

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 335,
            Panel1MinSize = 250,
            Panel2MinSize = 270
        };
        ConfigureGrid();
        split.Panel1.Controls.Add(_results);
        split.Panel2.Controls.Add(CreateLowerPanel());

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        var openProfile = new Button { Text = "Открыть профиль", AutoSize = true };
        openProfile.Click += (_, _) => OpenSelectedProfile();
        var reportButton = new Button { Text = "Сформировать сводный отчёт", AutoSize = true };
        reportButton.Click += (_, _) => PreviewSummaryReport();
        var saveButton = new Button { Text = "Сохранить сводку...", AutoSize = true };
        saveButton.Click += (_, _) => SaveSummaryReport();
        actions.Controls.Add(openProfile);
        actions.Controls.Add(reportButton);
        actions.Controls.Add(saveButton);

        root.Controls.Add(banner, 0, 0);
        root.Controls.Add(parameters, 0, 1);
        root.Controls.Add(split, 0, 2);
        root.Controls.Add(actions, 0, 3);
        Controls.Add(root);
    }

    private void ConfigureGrid()
    {
        _results.Dock = DockStyle.Fill;
        _results.ReadOnly = true;
        _results.AllowUserToAddRows = false;
        _results.AllowUserToDeleteRows = false;
        _results.AllowUserToResizeRows = false;
        _results.MultiSelect = false;
        _results.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _results.RowHeadersVisible = false;
        _results.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
        _results.Columns.Add("CrossSection", "CrossSection");
        _results.Columns.Add("Side", "Side");
        _results.Columns.Add("DepthCount", "DepthCount");
        _results.Columns.Add("VelocityCount", "VelocityCount");
        AddNumericColumn("Omega", "Omega");
        AddNumericColumn("Chi", "Chi");
        AddNumericColumn("R", "R");
        AddNumericColumn("B", "B");
        AddNumericColumn("vStar", "vStar");
        AddNumericColumn("C", "C");
        AddNumericColumn("VCalculated", "V_calculated");
        AddNumericColumn("Q", "Q");
        AddNumericColumn("ObservedVelocityMean", "ObservedVelocityMean");
        AddNumericColumn("Difference", "Difference");
        AddNumericColumn("RelativeDifference", "RelativeDifference");
        AddNumericColumn("Ratio", "Ratio");
        _results.SelectionChanged += (_, _) => UpdateSelectionFromGrid();
    }

    private void AddNumericColumn(string name, string header)
    {
        var column = new DataGridViewTextBoxColumn
        {
            Name = name,
            HeaderText = header,
            ValueType = typeof(double),
            DefaultCellStyle = { Format = "G8" }
        };
        _results.Columns.Add(column);
    }

    private Control CreateLowerPanel()
    {
        var lower = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 390,
            Panel1MinSize = 300,
            Panel2MinSize = 500
        };
        var detailGroup = new GroupBox
        {
            Text = "Выбранный профиль",
            Dock = DockStyle.Fill
        };
        _details.Dock = DockStyle.Fill;
        _details.FlowDirection = FlowDirection.TopDown;
        _details.WrapContents = false;
        _details.AutoScroll = true;
        _details.Padding = new Padding(10);
        detailGroup.Controls.Add(_details);
        lower.Panel1.Controls.Add(detailGroup);

        var charts = new TabControl { Dock = DockStyle.Fill };
        charts.TabPages.Add(CreateChartTab(
            "Сравнение скоростей",
            "Профиль",
            "Скорость, м/с",
            _renderModels.VelocityComparison,
            true));
        charts.TabPages.Add(CreateChartTab(
            "Q",
            "Профиль",
            "Q",
            _renderModels.Discharge,
            false));
        charts.TabPages.Add(CreateChartTab(
            "R",
            "Профиль",
            "HydraulicRadius",
            _renderModels.HydraulicRadius,
            false));
        lower.Panel2.Controls.Add(charts);
        return lower;
    }

    private static TabPage CreateChartTab(
        string title,
        string xTitle,
        string yTitle,
        System.Collections.Generic.IReadOnlyList<EducationalSummaryChartSeries> renderSeries,
        bool showLegend)
    {
        var tab = new TabPage(title);
        var chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.White };
        var area = new ChartArea("Summary")
        {
            BackColor = Color.White
        };
        area.AxisX.Title = xTitle;
        area.AxisX.Interval = 1;
        area.AxisX.MajorGrid.Enabled = false;
        area.AxisX.IsLabelAutoFit = true;
        area.AxisX.LabelAutoFitStyle = LabelAutoFitStyles.LabelsAngleStep45;
        area.AxisY.Title = yTitle;
        area.AxisY.MajorGrid.LineColor = Color.Gainsboro;
        chart.ChartAreas.Add(area);
        if (showLegend)
            chart.Legends.Add(new Legend("Series") { Docking = Docking.Top });

        var colors = new[] { Color.SteelBlue, Color.DarkOrange };
        for (var seriesIndex = 0; seriesIndex < renderSeries.Count; seriesIndex++)
        {
            var source = renderSeries[seriesIndex];
            var series = new Series(source.Name)
            {
                ChartArea = area.Name,
                ChartType = SeriesChartType.Column,
                XValueType = ChartValueType.String,
                YValueType = ChartValueType.Double,
                Color = colors[seriesIndex % colors.Length],
                IsVisibleInLegend = showLegend
            };
            foreach (var point in source.Points)
                series.Points.AddXY(point.ProfileLabel, point.Value);
            chart.Series.Add(series);
        }

        tab.Controls.Add(chart);
        return tab;
    }

    private void PopulateResults()
    {
        _results.Rows.Clear();
        foreach (var row in _model.Rows)
        {
            var index = _results.Rows.Add(
                row.CrossSectionNumber,
                row.Side,
                row.DepthCount,
                row.VelocityCount,
                row.Omega,
                row.Chi,
                row.HydraulicRadius,
                row.B,
                row.ShearVelocity,
                row.ChezyCoefficient,
                row.CalculatedVelocity,
                row.Discharge,
                row.ObservedVelocityMean,
                row.Difference,
                row.RelativeDifference,
                row.Ratio);
            _results.Rows[index].Tag = row.Profile;
        }
    }

    private void SelectRow(int crossSectionNumber, IsodatSide side)
    {
        foreach (DataGridViewRow row in _results.Rows)
        {
            if (row.Tag is EducationalHydraulicProfileData profile
                && profile.CrossSectionNumber == crossSectionNumber
                && profile.Side == side)
            {
                row.Selected = true;
                _results.CurrentCell = row.Cells[0];
                return;
            }
        }
    }

    private void UpdateSelectionFromGrid()
    {
        if (_results.CurrentRow?.Tag is not EducationalHydraulicProfileData profile)
            return;

        var selected = _selection.Select(profile.CrossSectionNumber, profile.Side);
        _selection = selected;
        PopulateDetails(selected.SelectedProfile);
    }

    private void PopulateDetails(EducationalHydraulicProfileData profile)
    {
        _details.Controls.Clear();
        var hydraulic = profile.HydraulicResult;
        var comparison = profile.VelocityComparison;
        AddDetail($"Створ: {profile.CrossSectionNumber}");
        AddDetail($"Сторона: {profile.Side}");
        AddDetail($"Omega: {Format(hydraulic.Omega)}");
        AddDetail($"Chi: {Format(hydraulic.Chi)}");
        AddDetail($"R: {Format(hydraulic.HydraulicRadius)}");
        AddDetail($"B: {Format(hydraulic.B)}");
        AddDetail($"vStar: {Format(hydraulic.ShearVelocity)}");
        AddDetail($"C: {Format(hydraulic.ChezyCoefficient)}");
        AddDetail($"V_calculated: {Format(hydraulic.CalculatedVelocity)}");
        AddDetail($"Q: {Format(hydraulic.Discharge)}");
        AddDetail($"ObservedVelocityMean: {Format(comparison.ObservedVelocityMean)}");
        AddDetail($"Difference: {Format(comparison.Difference)}");
        AddDetail($"RelativeDifference: {Format(comparison.RelativeDifference)}");
        AddDetail($"Ratio: {Format(comparison.Ratio)}");
    }

    private void AddDetail(string text) =>
        _details.Controls.Add(new Label
        {
            AutoSize = true,
            Margin = new Padding(4, 5, 4, 5),
            Text = text
        });

    private void OpenSelectedProfile()
    {
        using (var profileForm = new EducationalHydraulicForm(
            _model.Profiles,
            _selection.SelectedProfile.CrossSectionNumber,
            _selection.SelectedProfile.Side))
            profileForm.ShowDialog(this);
    }

    private void PreviewSummaryReport()
    {
        var html = _report.GenerateSummaryHtml(_model);
        using (var preview = new Form
        {
            Text = "Сводный учебный отчёт",
            Width = 1100,
            Height = 800,
            StartPosition = FormStartPosition.CenterParent
        })
        using (var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        })
        using (var browser = new WebBrowser { Dock = DockStyle.Fill, DocumentText = html })
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            var save = new Button
            {
                Text = "Сохранить отчёт...",
                AutoSize = true,
                Anchor = AnchorStyles.Right
            };
            save.Click += (_, _) => SaveSummaryReport();
            layout.Controls.Add(browser, 0, 0);
            layout.Controls.Add(save, 0, 1);
            preview.Controls.Add(layout);
            preview.ShowDialog(this);
        }
    }

    private void SaveSummaryReport()
    {
        using (var dialog = new SaveFileDialog
        {
            Title = "Сохранить сводный учебный отчёт",
            Filter = "HTML (*.html)|*.html|Текстовый файл (*.txt)|*.txt",
            FileName = "EducationalHydraulicSummary.html",
            AddExtension = true,
            DefaultExt = "html"
        })
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var content = Path.GetExtension(dialog.FileName)
                .Equals(".txt", StringComparison.OrdinalIgnoreCase)
                ? _report.GenerateSummaryText(_model)
                : _report.GenerateSummaryHtml(_model);
            File.WriteAllText(dialog.FileName, content, new UTF8Encoding(false));
        }
    }

    private static string Format(double value) =>
        value.ToString("G8", CultureInfo.InvariantCulture);

    private static string Format(double? value) =>
        value.HasValue ? Format(value.Value) : "не определено";
}
