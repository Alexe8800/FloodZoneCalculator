using System;

namespace FloodZoneDb.Client;

public sealed class IsodatHydraulicPoint
{
    public const string EducationalSyntheticInterpretation = "EducationalSynthetic";

    public long Id { get; }
    public int CrossSectionNumber { get; }
    public IsodatSide Side { get; }
    public decimal X { get; }
    public decimal? DepthH { get; }
    public decimal? ObservedVelocity { get; }
    public long SourceIsodatId { get; }
    public string SourceFileSha256 { get; }
    public string SourceSheet { get; }
    public int SourceRow { get; }
    public string SourceValueColumn { get; }
    public string Interpretation => EducationalSyntheticInterpretation;

    public IsodatHydraulicPoint(
        long id,
        int crossSectionNumber,
        IsodatSide side,
        decimal x,
        decimal? depthH,
        decimal? observedVelocity,
        long sourceIsodatId,
        string sourceFileSha256,
        string sourceSheet,
        int sourceRow,
        string sourceValueColumn)
    {
        if (id < 0)
            throw new ArgumentOutOfRangeException(nameof(id));
        if (crossSectionNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(crossSectionNumber));
        if (!Enum.IsDefined(typeof(IsodatSide), side))
            throw new ArgumentOutOfRangeException(nameof(side));
        if (sourceIsodatId <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceIsodatId));
        if (sourceFileSha256 == null || sourceFileSha256.Length != 64
            || !sourceFileSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("SHA-256 исходного файла должен содержать 64 шестнадцатеричных символа.",
                nameof(sourceFileSha256));
        if (string.IsNullOrWhiteSpace(sourceSheet))
            throw new ArgumentException("Лист источника не задан.", nameof(sourceSheet));
        if (sourceRow <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceRow));
        if (string.IsNullOrWhiteSpace(sourceValueColumn))
            throw new ArgumentException("Колонка исходного значения не задана.", nameof(sourceValueColumn));
        if (depthH.HasValue == observedVelocity.HasValue)
            throw new ArgumentException("Точка должна содержать ровно одно исходное значение.");

        Id = id;
        CrossSectionNumber = crossSectionNumber;
        Side = side;
        X = x;
        DepthH = depthH;
        ObservedVelocity = observedVelocity;
        SourceIsodatId = sourceIsodatId;
        SourceFileSha256 = sourceFileSha256;
        SourceSheet = sourceSheet;
        SourceRow = sourceRow;
        SourceValueColumn = sourceValueColumn;
    }
}
