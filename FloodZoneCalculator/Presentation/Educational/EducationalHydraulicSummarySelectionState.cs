#nullable enable
using System;
using System.Collections.Generic;
using FloodZoneDb.Client;

namespace FloodZoneCalculator.Presentation.Educational;

public sealed class EducationalHydraulicSummarySelectionState
{
    public EducationalHydraulicSummaryModel Model { get; }
    public EducationalHydraulicProfileData SelectedProfile { get; }

    public EducationalHydraulicSummarySelectionState(
        EducationalHydraulicSummaryModel model,
        int selectedCrossSectionNumber = 1,
        IsodatSide selectedSide = IsodatSide.Left)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        SelectedProfile = Model.GetProfile(selectedCrossSectionNumber, selectedSide);
    }

    public EducationalHydraulicSummarySelectionState Select(
        int crossSectionNumber,
        IsodatSide side) =>
        new(Model, crossSectionNumber, side);
}
