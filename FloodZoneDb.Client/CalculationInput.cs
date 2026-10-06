namespace FloodZoneDb.Client;

public sealed class CalculationInput
{
    public string N { get; set; } = "";
    public string ReservoirVolumeWv { get; set; } = "";
    public string ReservoirDepthHv { get; set; } = "";
    public string ReservoirAreaSv { get; set; } = "";
    public string ReservoirWidthBv { get; set; } = "";
    public string LowerReachDepthHb0 { get; set; } = "";
    public string LowerReachWidthBb0 { get; set; } = "";
    public string LowerReachVelocityVb0 { get; set; } = "";
    public string BreakDepthHr { get; set; } = "";
    public string DestructionDegreeEr { get; set; } = "";
    public string BreachThresholdP { get; set; } = "";
    public string WaterLevelZv { get; set; } = "";
}
