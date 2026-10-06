#nullable enable
using System;
using System.Linq;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation.Visualization;

public sealed class HydraulicVisualizationForm : Form
{
    private readonly HydraulicVisualizationState _state;
    private readonly ComboBox _sectionSelector = new();
    private readonly CrossSectionProfileControl _crossSectionProfile = new();

    public HydraulicVisualizationForm(HydraulicVisualizationModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        _state = new HydraulicVisualizationState(model);
        Text = "Гидравлическая визуализация";
        Width = 1180;
        Height = 820;
        MinimumSize = new System.Drawing.Size(900, 640);
        StartPosition = FormStartPosition.CenterParent;

        var tabs = new TabControl { Dock = DockStyle.Fill };
        var crossSectionTab = new TabPage("Поперечный профиль");
        var longitudinalTab = new TabPage("Продольный профиль");

        _sectionSelector.Dock = DockStyle.Fill;
        _sectionSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _sectionSelector.DisplayMember = nameof(CrossSectionSelectionItem.Caption);
        _sectionSelector.ValueMember = nameof(CrossSectionSelectionItem.Number);
        _sectionSelector.DataSource = _state.Selection.Sections.ToArray();
        _sectionSelector.SelectedIndexChanged += (_, _) =>
        {
            if (_sectionSelector.SelectedItem is CrossSectionSelectionItem selected)
            {
                _crossSectionProfile.Model = _state
                    .SelectSection(selected.Number)
                    .SelectedSection;
            }
        };

        var crossSectionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8)
        };
        crossSectionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        crossSectionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        crossSectionLayout.Controls.Add(_sectionSelector, 0, 0);
        crossSectionLayout.Controls.Add(_crossSectionProfile, 0, 1);
        crossSectionTab.Controls.Add(crossSectionLayout);

        var longitudinalProfile = new LongitudinalProfileControl
        {
            Dock = DockStyle.Fill,
            Model = model
        };
        longitudinalTab.Controls.Add(longitudinalProfile);

        tabs.TabPages.Add(crossSectionTab);
        tabs.TabPages.Add(longitudinalTab);
        Controls.Add(tabs);

        if (_state.SelectedSection != null)
        {
            _sectionSelector.SelectedValue = _state.SelectedSection.CrossSectionNumber;
            _crossSectionProfile.Model = _state.SelectedSection;
        }
        else
        {
            _sectionSelector.Enabled = false;
            _crossSectionProfile.Model = null;
        }
    }
}
