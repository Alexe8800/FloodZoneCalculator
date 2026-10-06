using System;
using System.Collections.Generic;
using System.Linq;
using FloodZoneCalculator.Domain;
using FloodZoneDb.Client;

namespace FloodZoneCalculator.Presentation.Educational;

public sealed class EducationalHydraulicProfileData
{
    public EducationalDepthProfile DepthProfile { get; }
    public IReadOnlyList<IsodatHydraulicPoint> SourcePoints { get; }
    public EducationalFullHydraulicResult HydraulicResult { get; }
    public EducationalVelocityComparisonResult VelocityComparison { get; }
    public int CrossSectionNumber => DepthProfile.CrossSectionNumber;
    public IsodatSide Side => DepthProfile.Side;
    public IReadOnlyList<EducationalDepthPoint> DepthPoints => DepthProfile.Points;
    public IReadOnlyList<IsodatHydraulicPoint> VelocityPoints { get; }

    public EducationalHydraulicProfileData(
        EducationalDepthProfile depthProfile,
        IEnumerable<IsodatHydraulicPoint> sourcePoints,
        EducationalFullHydraulicResult hydraulicResult,
        EducationalVelocityComparisonResult velocityComparison)
    {
        DepthProfile = depthProfile ?? throw new ArgumentNullException(nameof(depthProfile));
        if (sourcePoints == null)
            throw new ArgumentNullException(nameof(sourcePoints));
        HydraulicResult = hydraulicResult ?? throw new ArgumentNullException(nameof(hydraulicResult));
        VelocityComparison = velocityComparison
            ?? throw new ArgumentNullException(nameof(velocityComparison));

        if (hydraulicResult.CrossSectionNumber != depthProfile.CrossSectionNumber
            || hydraulicResult.Side != depthProfile.Side
            || velocityComparison.CrossSectionNumber != depthProfile.CrossSectionNumber
            || velocityComparison.Side != depthProfile.Side)
            throw new ArgumentException("Готовые результаты должны соответствовать Depth-профилю.");

        var materialized = sourcePoints.ToArray();
        if (materialized.Any(point => point == null))
            throw new ArgumentException("Исходный набор содержит пустую точку.", nameof(sourcePoints));
        if (materialized.Any(point =>
                point.CrossSectionNumber != depthProfile.CrossSectionNumber
                || point.Side != depthProfile.Side))
            throw new ArgumentException("Все исходные точки должны принадлежать выбранному створу и стороне.",
                nameof(sourcePoints));

        var sourceDepths = materialized.Where(point => point.DepthH.HasValue).ToArray();
        if (sourceDepths.Length != depthProfile.Points.Count
            || !sourceDepths.Select(point => point.SourceIsodatId).OrderBy(id => id)
                .SequenceEqual(depthProfile.Points.Select(point => point.SourceIsodatId).OrderBy(id => id)))
            throw new ArgumentException("Исходные Depth-точки не соответствуют переданному профилю.",
                nameof(sourcePoints));

        var sourceVelocities = materialized.Where(point => point.ObservedVelocity.HasValue).ToArray();
        if (sourceVelocities.Length != velocityComparison.ObservedCount)
            throw new ArgumentException("Количество исходных Velocity-точек не совпадает с результатом сравнения.",
                nameof(sourcePoints));

        SourcePoints = Array.AsReadOnly(materialized);
        VelocityPoints = Array.AsReadOnly(sourceVelocities);
    }
}
