#nullable enable
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using FloodZoneDb.Client;

namespace FloodZoneCalculator.Presentation.Educational;

public sealed class EducationalHydraulicForm : Form
{
    private readonly EducationalHydraulicSelectionState _initialState;
    private readonly EducationalDepthProfileRenderer _depthRenderer = new();
    private readonly EducationalVelocityProfileRenderer _velocityRenderer = new();
    private readonly EducationalHydraulicReport _report = new();
    private readonly ComboBox _sectionSelector = new();
    private readonly ComboBox _sideSelector = new();
    private readonly Label _profileSummary = new();
    private readonly EducationalProfileChartControl _depthChart = new();
    private readonly EducationalProfileChartControl _velocityChart = new();
    private readonly DataGridView _sourceTable = new();
    private readonly FlowLayoutPanel _comparisonValues = new();
    private readonly FlowLayoutPanel _hydraulicValues = new();
    private EducationalHydraulicSelectionState _state;

    public EducationalHydraulicForm(
        System.Collections.Generic.IEnumerable<EducationalHydraulicProfileData> profiles,
        int? selectedCrossSectionNumber = null,
        IsodatSide? selectedSide = null)
    {
        _initialState = new EducationalHydraulicSelectionState(
            profiles, selectedCrossSectionNumber, selectedSide);
        _state = _initialState;
        Text = "Учебная гидравлическая модель";
        Width = 1280;
        Height = 900;
        MinimumSize = new Size(980, 680);
        StartPosition = FormStartPosition.CenterParent;

        BuildLayout();
        SelectCurrentProfile();
    }

    public int SelectedCrossSectionNumber => _state.SelectedProfile.CrossSectionNumber;
    public IsodatSide SelectedSide => _state.SelectedProfile.Side;

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var selectors = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        selectors.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        selectors.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        selectors.Controls.Add(new Label { Text = "Створ:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
        selectors.Controls.Add(_sectionSelector, 1, 0);
        selectors.Controls.Add(new Label { Text = "Сторона:", TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 2, 0);
        selectors.Controls.Add(_sideSelector, 3, 0);

        _sectionSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _sectionSelector.Dock = DockStyle.Fill;
        _sectionSelector.DataSource = _initialState.CrossSectionNumbers.ToArray();
        _sideSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _sideSelector.Dock = DockStyle.Fill;
        _sideSelector.DataSource = _initialState.Sides.ToArray();
        _sectionSelector.SelectedItem = _initialState.SelectedProfile.CrossSectionNumber;
        _sideSelector.SelectedItem = _initialState.SelectedProfile.Side;
        _sectionSelector.SelectedIndexChanged += (_, _) => UpdateSelection();
        _sideSelector.SelectedIndexChanged += (_, _) => UpdateSelection();

        _profileSummary.Dock = DockStyle.Fill;
        _profileSummary.TextAlign = ContentAlignment.MiddleLeft;
        _profileSummary.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreateProfileTab());
        tabs.TabPages.Add(CreateVelocityTab());
        tabs.TabPages.Add(CreateHydraulicTab());
        tabs.TabPages.Add(CreateSourceTab());

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Height = 38
        };
        var saveButton = new Button { Text = "Сохранить отчёт...", AutoSize = true };
        saveButton.Click += (_, _) => SaveReport();
        var reportButton = new Button { Text = "Сформировать отчёт", AutoSize = true };
        reportButton.Click += (_, _) => PreviewReport();
        var summaryButton = new Button { Text = "Сводка по 6 профилям", AutoSize = true };
        summaryButton.Click += (_, _) =>
        {
            using (var summary = new EducationalHydraulicSummaryForm(_initialState.Profiles))
                summary.ShowDialog(this);
        };
        actions.Controls.Add(saveButton);
        actions.Controls.Add(reportButton);
        actions.Controls.Add(summaryButton);

        var content = new Panel { Dock = DockStyle.Fill };
        content.Controls.Add(tabs);
        content.Controls.Add(actions);
        actions.Dock = DockStyle.Bottom;
        tabs.Dock = DockStyle.Fill;

        root.Controls.Add(selectors, 0, 0);
        root.Controls.Add(_profileSummary, 0, 1);
        root.Controls.Add(content, 0, 2);
        Controls.Add(root);
    }

    private TabPage CreateProfileTab()
    {
        var tab = new TabPage("Профиль");
        _depthChart.Dock = DockStyle.Fill;
        tab.Controls.Add(_depthChart);
        return tab;
    }

    private TabPage CreateVelocityTab()
    {
        var tab = new TabPage("Скорость");
        _velocityChart.Dock = DockStyle.Fill;
        tab.Controls.Add(_velocityChart);
        return tab;
    }

    private TabPage CreateHydraulicTab()
    {
        var tab = new TabPage("Гидравлика");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        ConfigureValuesPanel(_hydraulicValues);
        ConfigureValuesPanel(_comparisonValues);
        var calculationGroup = new GroupBox { Text = "Учебный гидравлический расчёт", Dock = DockStyle.Fill };
        calculationGroup.Controls.Add(_hydraulicValues);
        var comparisonGroup = new GroupBox { Text = "Сравнение скоростей", Dock = DockStyle.Fill };
        var comparisonLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        comparisonLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        comparisonLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        comparisonLayout.Controls.Add(new Label
        {
            Text = "Сравнение является учебным и не является верификацией методики.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.DarkRed
        }, 0, 0);
        comparisonLayout.Controls.Add(_comparisonValues, 0, 1);
        comparisonGroup.Controls.Add(comparisonLayout);
        layout.Controls.Add(calculationGroup, 0, 0);
        layout.Controls.Add(comparisonGroup, 0, 1);
        tab.Controls.Add(layout);
        return tab;
    }

    private static void ConfigureValuesPanel(FlowLayoutPanel values)
    {
        values.Dock = DockStyle.Fill;
        values.FlowDirection = FlowDirection.TopDown;
        values.WrapContents = false;
        values.AutoScroll = true;
        values.Padding = new Padding(10);
    }

    private TabPage CreateSourceTab()
    {
        var tab = new TabPage("Исходные данные");
        _sourceTable.Dock = DockStyle.Fill;
        _sourceTable.ReadOnly = true;
        _sourceTable.AllowUserToAddRows = false;
        _sourceTable.AllowUserToDeleteRows = false;
        _sourceTable.AllowUserToResizeRows = false;
        _sourceTable.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _sourceTable.RowHeadersVisible = false;
        _sourceTable.Columns.Add("type", "Тип");
        _sourceTable.Columns.Add("x", "X — Ri / DistanceM");
        _sourceTable.Columns.Add("value", "Значение");
        _sourceTable.Columns.Add("sourceId", "SourceIsodatId");
        tab.Controls.Add(_sourceTable);
        return tab;
    }

    private void UpdateSelection()
    {
        if (_sectionSelector.SelectedItem is int section
            && _sideSelector.SelectedItem is IsodatSide side)
        {
            _state = _state.Select(section, side);
            SelectCurrentProfile();
        }
    }

    private void SelectCurrentProfile()
    {
        var profile = _state.SelectedProfile;
        _profileSummary.Text = $"Створ №{profile.CrossSectionNumber} | {SideCaption(profile.Side)} | "
            + $"Depth-точек: {profile.DepthPoints.Count} | Velocity-точек: {profile.VelocityPoints.Count}";
        _depthChart.Frame = _depthRenderer.Prepare(profile);
        _velocityChart.Frame = _velocityRenderer.Prepare(profile);
        PopulateHydraulicValues(profile);
        PopulateComparisonValues(profile);
        PopulateSourceTable(profile);
    }

    private void PopulateHydraulicValues(EducationalHydraulicProfileData profile)
    {
        _hydraulicValues.Controls.Clear();
        var result = profile.HydraulicResult;
        AddValue(_hydraulicValues, "Omega, м²", result.Omega);
        AddValue(_hydraulicValues, "Chi, м", result.Chi);
        AddValue(_hydraulicValues, "HydraulicRadius, м", result.HydraulicRadius);
        AddValue(_hydraulicValues, "B, м — учебный параметр", result.B);
        AddValue(_hydraulicValues, "g, м/с² — учебный параметр", result.Gravity);
        AddValue(_hydraulicValues, "nu, м²/с — учебный параметр", result.KinematicViscosity);
        AddValue(_hydraulicValues, "h, м — учебный параметр", result.EducationalWaterDepth);
        AddValue(_hydraulicValues, "If — учебный параметр", result.HydraulicSlope);
        AddValue(_hydraulicValues, "vStar, м/с", result.ShearVelocity);
        AddValue(_hydraulicValues, "C", result.ChezyCoefficient);
        AddValue(_hydraulicValues, "V_calculated, м/с", result.CalculatedVelocity);
        AddValue(_hydraulicValues, "Q, м³/с", result.Discharge);
    }

    private void PopulateComparisonValues(EducationalHydraulicProfileData profile)
    {
        _comparisonValues.Controls.Clear();
        var result = profile.VelocityComparison;
        AddValue(_comparisonValues, "Средняя наблюдаемая скорость, м/с", result.ObservedVelocityMean);
        AddValue(_comparisonValues, "Расчётная скорость, м/с", result.CalculatedVelocity);
        AddValue(_comparisonValues, "Разница, м/с", result.Difference);
        AddValue(_comparisonValues, "Относительное расхождение", result.RelativeDifference);
        AddValue(_comparisonValues, "Отношение", result.Ratio);
    }

    private void PopulateSourceTable(EducationalHydraulicProfileData profile)
    {
        _sourceTable.Rows.Clear();
        foreach (var point in profile.DepthPoints)
            _sourceTable.Rows.Add(
                "Depth",
                Format((double)point.X),
                Format((double)point.DepthH),
                point.SourceIsodatId);
        foreach (var point in profile.VelocityPoints)
            _sourceTable.Rows.Add(
                "ObservedVelocity",
                Format((double)point.X),
                Format((double)point.ObservedVelocity!.Value),
                point.SourceIsodatId);
    }

    private void PreviewReport()
    {
        var html = _report.GenerateHtml(_state.SelectedProfile);
        using (var preview = new Form
        {
            Text = "Предпросмотр учебного отчёта",
            Width = 980,
            Height = 760,
            StartPosition = FormStartPosition.CenterParent
        })
        using (var browser = new WebBrowser { Dock = DockStyle.Fill, DocumentText = html })
        {
            preview.Controls.Add(browser);
            preview.ShowDialog(this);
        }
    }

    private void SaveReport()
    {
        var html = _report.GenerateHtml(_state.SelectedProfile);
        using (var dialog = new SaveFileDialog
        {
            Title = "Сохранить учебный отчёт",
            Filter = "HTML (*.html)|*.html|Текстовый файл (*.txt)|*.txt",
            FileName = $"EducationalHydraulicReport_{_state.SelectedProfile.CrossSectionNumber}_{_state.SelectedProfile.Side}.html",
            AddExtension = true,
            DefaultExt = "html"
        })
        {
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            var content = Path.GetExtension(dialog.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase)
                ? _report.GenerateText(_state.SelectedProfile)
                : html;
            File.WriteAllText(dialog.FileName, content, new UTF8Encoding(false));
        }
    }

    private static void AddValue(FlowLayoutPanel panel, string caption, double value) =>
        AddValue(panel, caption, (double?)value);

    private static void AddValue(FlowLayoutPanel panel, string caption, double? value)
    {
        panel.Controls.Add(new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 10F),
            Margin = new Padding(4, 5, 4, 5),
            Text = caption + ": " + (value.HasValue ? Format(value.Value) : "не определено")
        });
    }

    private static string Format(double value) =>
        value.ToString("G10", System.Globalization.CultureInfo.InvariantCulture);

    private static string SideCaption(IsodatSide side) =>
        side == IsodatSide.Left ? "Левый берег" : "Правый берег";
}
