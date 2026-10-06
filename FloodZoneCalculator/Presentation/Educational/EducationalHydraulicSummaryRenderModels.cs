#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace FloodZoneCalculator.Presentation.Educational;

public sealed class EducationalSummaryChartPoint
{
    public EducationalSummaryChartPoint(string profileLabel, double value)
    {
        ProfileLabel = profileLabel ?? throw new ArgumentNullException(nameof(profileLabel));
        Value = value;
    }

    public string ProfileLabel { get; }
    public double Value { get; }
}

public sealed class EducationalSummaryChartSeries
{
    public EducationalSummaryChartSeries(
        string name,
        IEnumerable<EducationalSummaryChartPoint> points)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Points = Array.AsReadOnly(points.ToArray());
    }

    public string Name { get; }
    public IReadOnlyList<EducationalSummaryChartPoint> Points { get; }
}

public sealed class EducationalHydraulicSummaryRenderModels
{
    public IReadOnlyList<EducationalSummaryChartSeries> VelocityComparison { get; }
    public IReadOnlyList<EducationalSummaryChartSeries> Discharge { get; }
    public IReadOnlyList<EducationalSummaryChartSeries> HydraulicRadius { get; }

    public EducationalHydraulicSummaryRenderModels(EducationalHydraulicSummaryModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        var labels = model.Rows.Select(row =>
            $"{row.CrossSectionNumber} {row.Side}").ToArray();
        VelocityComparison = Array.AsReadOnly(new[]
        {
            new EducationalSummaryChartSeries("ObservedVelocityMean",
                model.Rows.Select((row, index) =>
                    new EducationalSummaryChartPoint(labels[index], row.ObservedVelocityMean))),
            new EducationalSummaryChartSeries("V_calculated",
                model.Rows.Select((row, index) =>
                    new EducationalSummaryChartPoint(labels[index], row.CalculatedVelocity)))
        });
        Discharge = Array.AsReadOnly(new[]
        {
            new EducationalSummaryChartSeries("Q",
                model.Rows.Select((row, index) =>
                    new EducationalSummaryChartPoint(labels[index], row.Discharge)))
        });
        HydraulicRadius = Array.AsReadOnly(new[]
        {
            new EducationalSummaryChartSeries("R",
                model.Rows.Select((row, index) =>
                    new EducationalSummaryChartPoint(labels[index], row.HydraulicRadius)))
        });
    }
}
