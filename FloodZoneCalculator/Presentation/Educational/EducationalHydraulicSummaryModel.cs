#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FloodZoneDb.Client;

namespace FloodZoneCalculator.Presentation.Educational;

public sealed class EducationalHydraulicSummaryRow
{
    internal EducationalHydraulicSummaryRow(EducationalHydraulicProfileData profile)
    {
        Profile = profile;
        CrossSectionNumber = profile.CrossSectionNumber;
        Side = profile.Side;
        DepthCount = profile.DepthPoints.Count;
        VelocityCount = profile.VelocityPoints.Count;
        Omega = profile.HydraulicResult.Omega;
        Chi = profile.HydraulicResult.Chi;
        HydraulicRadius = profile.HydraulicResult.HydraulicRadius;
        B = profile.HydraulicResult.B;
        ShearVelocity = profile.HydraulicResult.ShearVelocity;
        ChezyCoefficient = profile.HydraulicResult.ChezyCoefficient;
        CalculatedVelocity = profile.HydraulicResult.CalculatedVelocity;
        Discharge = profile.HydraulicResult.Discharge;
        ObservedVelocityMean = profile.VelocityComparison.ObservedVelocityMean;
        Difference = profile.VelocityComparison.Difference;
        RelativeDifference = profile.VelocityComparison.RelativeDifference;
        Ratio = profile.VelocityComparison.Ratio;
    }

    public EducationalHydraulicProfileData Profile { get; }
    public int CrossSectionNumber { get; }
    public IsodatSide Side { get; }
    public int DepthCount { get; }
    public int VelocityCount { get; }
    public double Omega { get; }
    public double Chi { get; }
    public double HydraulicRadius { get; }
    public double B { get; }
    public double ShearVelocity { get; }
    public double ChezyCoefficient { get; }
    public double CalculatedVelocity { get; }
    public double Discharge { get; }
    public double ObservedVelocityMean { get; }
    public double Difference { get; }
    public double? RelativeDifference { get; }
    public double? Ratio { get; }
}

public sealed class EducationalHydraulicSummaryModel
{
    private readonly IReadOnlyDictionary<(int CrossSectionNumber, IsodatSide Side), EducationalHydraulicProfileData>
        _profilesByKey;

    public IReadOnlyList<EducationalHydraulicProfileData> Profiles { get; }
    public IReadOnlyList<EducationalHydraulicSummaryRow> Rows { get; }

    public EducationalHydraulicSummaryModel(IEnumerable<EducationalHydraulicProfileData>? profiles)
    {
        if (profiles == null)
            throw new ArgumentNullException(nameof(profiles));

        var input = profiles.ToArray();
        if (input.Any(profile => profile == null))
            throw new ArgumentException("Список содержит пустой профиль.", nameof(profiles));

        var expectedKeys = (from section in new[] { 1, 2, 3 }
                            from side in new[] { IsodatSide.Left, IsodatSide.Right }
                            select (CrossSectionNumber: section, Side: side)).ToArray();
        var indexed = input.ToDictionary(
            profile => (profile.CrossSectionNumber, profile.Side));
        if (indexed.Count != expectedKeys.Length
            || expectedKeys.Any(key => !indexed.ContainsKey(key)))
            throw new ArgumentException(
                "Нужны ровно шесть готовых профилей: створы 1/2/3, Left/Right.",
                nameof(profiles));

        var ordered = expectedKeys.Select(key => indexed[key]).ToArray();
        Profiles = Array.AsReadOnly(ordered);
        Rows = Array.AsReadOnly(ordered.Select(profile =>
            new EducationalHydraulicSummaryRow(profile)).ToArray());
        _profilesByKey = new System.Collections.ObjectModel.ReadOnlyDictionary<
            (int CrossSectionNumber, IsodatSide Side), EducationalHydraulicProfileData>(indexed);
    }

    public EducationalHydraulicProfileData GetProfile(int crossSectionNumber, IsodatSide side)
    {
        if (!_profilesByKey.TryGetValue((crossSectionNumber, side), out var profile))
            throw new KeyNotFoundException(
                $"Нет подготовленного профиля для створа {crossSectionNumber}, {side}.");

        return profile;
    }
}
