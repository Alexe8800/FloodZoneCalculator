using System;
using System.Collections.Generic;
using System.Linq;
using FloodZoneDb.Client;

namespace FloodZoneCalculator.Presentation.Educational;

public sealed class EducationalHydraulicSelectionState
{
    private readonly IReadOnlyDictionary<(int CrossSectionNumber, IsodatSide Side), EducationalHydraulicProfileData>
        _profiles;

    public IReadOnlyList<int> CrossSectionNumbers { get; }
    public IReadOnlyList<IsodatSide> Sides { get; }
    public IReadOnlyList<EducationalHydraulicProfileData> Profiles { get; }
    public EducationalHydraulicProfileData SelectedProfile { get; }

    public EducationalHydraulicSelectionState(
        IEnumerable<EducationalHydraulicProfileData> profiles,
        int? selectedCrossSectionNumber = null,
        IsodatSide? selectedSide = null)
    {
        if (profiles == null)
            throw new ArgumentNullException(nameof(profiles));

        var materialized = profiles.ToArray();
        if (materialized.Length == 0 || materialized.Any(profile => profile == null))
            throw new ArgumentException("Нужно передать готовые учебные профили.", nameof(profiles));

        var byKey = materialized.ToDictionary(
            profile => (profile.CrossSectionNumber, profile.Side));
        var sectionNumbers = materialized.Select(profile => profile.CrossSectionNumber)
            .Distinct().OrderBy(number => number).ToArray();
        var sides = materialized.Select(profile => profile.Side).Distinct().OrderBy(side => side).ToArray();

        if (!sectionNumbers.SequenceEqual(new[] { 1, 2, 3 })
            || !sides.SequenceEqual(Enum.GetValues(typeof(IsodatSide)).Cast<IsodatSide>().OrderBy(side => side))
            || byKey.Count != 6)
            throw new ArgumentException("Учебная форма ожидает готовые профили створов 1/2/3 для Left/Right.",
                nameof(profiles));

        var selectedNumber = selectedCrossSectionNumber ?? sectionNumbers[0];
        var selectedSideValue = selectedSide ?? sides[0];
        if (!byKey.TryGetValue((selectedNumber, selectedSideValue), out var selected))
            throw new ArgumentOutOfRangeException(
                selectedCrossSectionNumber.HasValue ? nameof(selectedCrossSectionNumber) : nameof(selectedSide),
                "Запрошенный профиль отсутствует в подготовленных данных.");

        _profiles = byKey;
        CrossSectionNumbers = Array.AsReadOnly(sectionNumbers);
        Sides = Array.AsReadOnly(sides);
        Profiles = Array.AsReadOnly(materialized
            .OrderBy(profile => profile.CrossSectionNumber)
            .ThenBy(profile => profile.Side)
            .ToArray());
        SelectedProfile = selected;
    }

    public EducationalHydraulicSelectionState Select(int crossSectionNumber, IsodatSide side)
    {
        if (!_profiles.TryGetValue((crossSectionNumber, side), out var selected))
            throw new ArgumentOutOfRangeException(
                nameof(crossSectionNumber),
                $"Нет подготовленного профиля для створа {crossSectionNumber}, {side}.");

        return new EducationalHydraulicSelectionState(
            _profiles.Values,
            selected.CrossSectionNumber,
            selected.Side);
    }
}
