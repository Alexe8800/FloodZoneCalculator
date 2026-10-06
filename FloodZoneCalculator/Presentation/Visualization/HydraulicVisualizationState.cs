#nullable enable
using System;
using System.Linq;

namespace FloodZoneCalculator.Presentation.Visualization;

public sealed class HydraulicVisualizationState
{
    public HydraulicVisualizationState(
        HydraulicVisualizationModel model,
        int? selectedCrossSectionNumber = null)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Selection = new CrossSectionSelectionModel(model);

        if (selectedCrossSectionNumber.HasValue)
        {
            SelectedSection = Selection.GetSection(selectedCrossSectionNumber.Value);
        }
        else
        {
            SelectedSection = Selection.Sections.FirstOrDefault()?.Model;
        }
    }

    public HydraulicVisualizationModel Model { get; }
    public CrossSectionSelectionModel Selection { get; }
    public CrossSectionVisualizationModel? SelectedSection { get; }

    public HydraulicVisualizationState SelectSection(int number)
    {
        _ = Selection.GetSection(number);
        return new HydraulicVisualizationState(Model, number);
    }
}
