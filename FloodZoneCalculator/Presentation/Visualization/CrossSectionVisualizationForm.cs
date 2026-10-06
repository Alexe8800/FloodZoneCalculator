#nullable enable
using System;
using System.Linq;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation.Visualization;

public sealed class CrossSectionVisualizationForm : Form
{
    private readonly ComboBox _sectionSelector = new();
    private readonly CrossSectionProfileControl _profile = new();

    public CrossSectionVisualizationForm(HydraulicVisualizationModel model)
        : this(GetSections(model))
    {
    }

    public CrossSectionVisualizationForm(CrossSectionVisualizationModel model)
        : this(new[] { model ?? throw new ArgumentNullException(nameof(model)) })
    {
    }

    private CrossSectionVisualizationForm(
        CrossSectionVisualizationModel[] crossSections)
    {
        if (crossSections.Length == 0)
            throw new ArgumentException(
                "Для отображения необходим хотя бы один поперечный створ.",
                nameof(crossSections));

        Text = "Поперечный профиль";
        Width = 1100;
        Height = 760;
        StartPosition = FormStartPosition.CenterParent;

        _sectionSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _sectionSelector.Dock = DockStyle.Fill;
        _sectionSelector.DisplayMember = nameof(SectionChoice.Caption);
        _sectionSelector.ValueMember = nameof(SectionChoice.Model);
        _sectionSelector.DataSource = crossSections
            .Select(section => new SectionChoice(section))
            .ToArray();
        _sectionSelector.SelectedIndexChanged += (_, _) =>
        {
            if (_sectionSelector.SelectedItem is SectionChoice selected)
                _profile.Model = selected.Model;
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(_sectionSelector, 0, 0);
        layout.Controls.Add(_profile, 0, 1);
        Controls.Add(layout);

        _sectionSelector.SelectedIndex = 0;
        _profile.Model = crossSections[0];
    }

    private static CrossSectionVisualizationModel[] GetSections(
        HydraulicVisualizationModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        return model.CrossSections.ToArray();
    }

    private sealed class SectionChoice
    {
        public SectionChoice(CrossSectionVisualizationModel model)
        {
            Model = model;
            Caption = "Створ №" + model.CrossSectionNumber;
        }

        public string Caption { get; }
        public CrossSectionVisualizationModel Model { get; }
    }
}
