namespace FloodZoneDb.Client;

public sealed class CrossSectionRecord
{
    public long Id { get; set; }
    public long ObjectId { get; set; }
    public int Number { get; set; }
    public string DistanceFromHydroUnitM { get; set; } = "";
    public string BottomLevelZb { get; set; } = "";
    public string DepthHb { get; set; } = "";
    public string WidthBb { get; set; } = "";
    public string VelocityVb { get; set; } = "";
    public string Kgm { get; set; } = "";
    public string LeftBankHeightM { get; set; } = "";
    public string LeftFloodplainWidthM { get; set; } = "";
    public string RightBankHeightM { get; set; } = "";
    public string RightFloodplainWidthM { get; set; } = "";
    public List<BankPointRecord> BankPoints { get; set; } = new();
}

public sealed class BankPointRecord
{
    public long Id { get; set; }
    public long CrossSectionId { get; set; }
    public string Side { get; set; } = "";
    public string PointType { get; set; } = "";
    public int PointNumber { get; set; }
    public string DistanceM { get; set; } = "";
    public string ElevationM { get; set; } = "";
}

public enum ProfileSide
{
    Left,
    Center,
    Right
}

public enum ProfilePointType
{
    Bank,
    Horizontal,
    ChannelBank,
    Bottom
}
