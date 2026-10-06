#nullable enable
using System;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation.Visualization;

public sealed class CrossSectionSelectionForm : Form
{
    private readonly CrossSectionSelectionModel _selection;
    private readonly ListBox _sections = new();

    public CrossSectionSelectionForm(HydraulicVisualizationModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        _selection = new CrossSectionSelectionModel(model);
        if (_selection.Sections.Count == 0)
            throw new ArgumentException(
                "Для выбора необходим хотя бы один поперечный створ.",
                nameof(model));

        Text = "Выбор поперечного створа";
        Width = 440;
        Height = 440;
        StartPosition = FormStartPosition.CenterParent;

        _sections.Dock = DockStyle.Fill;
        _sections.IntegralHeight = false;
        _sections.DisplayMember = nameof(CrossSectionSelectionItem.Caption);
        _sections.DataSource = _selection.Sections;
        _sections.SelectedIndex = 0;
        _sections.DoubleClick += (_, _) => ShowSelectedSection();

        var showButton = new Button
        {
            Text = "Показать",
            Dock = DockStyle.Right,
            Width = 110
        };
        showButton.Click += (_, _) => ShowSelectedSection();

        var buttons = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Padding = new Padding(4)
        };
        buttons.Controls.Add(showButton);

        Controls.Add(_sections);
        Controls.Add(buttons);
    }

    private void ShowSelectedSection()
    {
        if (_sections.SelectedItem is not CrossSectionSelectionItem selected)
            return;

        using var viewer = new CrossSectionVisualizationForm(selected.Model);
        viewer.ShowDialog(this);
    }
}
