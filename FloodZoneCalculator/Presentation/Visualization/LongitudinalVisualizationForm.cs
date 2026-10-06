#nullable enable
using System;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation.Visualization;

public sealed class LongitudinalVisualizationForm : Form
{
    public LongitudinalVisualizationForm(HydraulicVisualizationModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        Text = "Продольная геометрия створов";
        Width = 1100;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;

        var control = new LongitudinalProfileControl
        {
            Dock = DockStyle.Fill,
            Model = model
        };
        Controls.Add(control);
    }
}
