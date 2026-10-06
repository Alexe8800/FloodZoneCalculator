using System;
using System.Collections.Generic;
using System.Linq;

namespace FloodZoneCalculator.Presentation.Visualization;

public sealed class CrossSectionSelectionItem
{
    public CrossSectionSelectionItem(CrossSectionVisualizationModel model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        Number = model.CrossSectionNumber;
        DistanceFromHydroUnitM = model.DistanceFromHydroUnitM;
        Caption = "Створ №" + Number + " @ " +
            DistanceFromHydroUnitM.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    }

    public int Number { get; }
    public double DistanceFromHydroUnitM { get; }
    public string Caption { get; }
    public CrossSectionVisualizationModel Model { get; }
}

public sealed class CrossSectionSelectionModel
{
    private readonly IReadOnlyDictionary<int, CrossSectionSelectionItem> _itemsByNumber;

    public CrossSectionSelectionModel(HydraulicVisualizationModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        var items = model.CrossSections
            .Select(section => new CrossSectionSelectionItem(section))
            .OrderBy(item => item.DistanceFromHydroUnitM)
            .ToArray();

        if (items.Select(item => item.Number).Distinct().Count() != items.Length)
            throw new ArgumentException(
                "Номера створов в модели выбора должны быть уникальными.",
                nameof(model));

        Sections = Array.AsReadOnly(items);
        _itemsByNumber = items.ToDictionary(item => item.Number);
    }

    public IReadOnlyList<CrossSectionSelectionItem> Sections { get; }

    public CrossSectionVisualizationModel GetSection(int number)
    {
        if (!_itemsByNumber.TryGetValue(number, out var item))
            throw new ArgumentOutOfRangeException(nameof(number), number,
                "Створ отсутствует в модели выбора.");

        return item.Model;
    }
}
