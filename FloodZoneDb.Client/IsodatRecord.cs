namespace FloodZoneDb.Client;

public enum IsodatSide
{
    Left,
    Right
}

public enum IsodatType
{
    Depth,
    Velocity,
    CalculatedFloodDuration,
    ActualFloodDuration
}

public sealed class IsodatRecord
{
    public long Id { get; set; }
    public int CrossSectionNumber { get; set; }
    public IsodatSide Side { get; set; }
    public IsodatType IsodatType { get; set; }
    public decimal Value { get; set; }
    public decimal DistanceM { get; set; }
    public string SourceSheet { get; set; } = "";
    public string SourceFileName { get; set; } = "";
    public string SourceFileSha256 { get; set; } = "";
    public int SourceRow { get; set; }
    public string ValueSourceColumn { get; set; } = "";
    public string DistanceSourceColumn { get; set; } = "";
}
