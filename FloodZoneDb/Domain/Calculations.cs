using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FloodZoneDb.Domain;

[Table("calculations")]
public class Calculation
{
    [Key] public long Id { get; set; }
    public long ObjectId { get; set; }
    [ForeignKey(nameof(ObjectId))] public GtsObject? Object { get; set; }
    [MaxLength(50)] public string CalcNumber { get; set; } = null!;
    [MaxLength(300)] public string Name { get; set; } = null!;
    [MaxLength(50)] public string Mode { get; set; } = "view";
    public CalcStatus Status { get; set; } = CalcStatus.Draft;
    public int? PerformedByUserId { get; set; }
    [ForeignKey(nameof(PerformedByUserId))] public User? PerformedBy { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

[Table("calc_main_results")]
public class CalcMainResult
{
    [Key] public long CalculationId { get; set; }
    [ForeignKey(nameof(CalculationId))] public Calculation? Calculation { get; set; }
    public decimal? ThroughputM3s { get; set; }
    public decimal? MaxFlowM3s { get; set; }
    public decimal? MinFlowM3s { get; set; }
    public decimal? AvgVelocityMs { get; set; }
    public decimal? UpstreamLevelM { get; set; }
    public decimal? DownstreamLevelM { get; set; }
    public decimal? EfficiencyPct { get; set; }
    public decimal? AnnualGenerationMkwh { get; set; }
    public decimal? ReliabilityCoeff { get; set; }
}

[Table("calc_hydrological")]
public class CalcHydrological
{
    [Key] public long CalculationId { get; set; }
    [ForeignKey(nameof(CalculationId))] public Calculation? Calculation { get; set; }
    public decimal? CatchmentAreaKm2 { get; set; }
    public decimal? AvgAnnualRunoffKm3 { get; set; }
    public decimal? MaxFlowM3s { get; set; }
    public decimal? MinFlowM3s { get; set; }
    public decimal? ProvisionPct { get; set; }
}

[Table("calc_structural")]
public class CalcStructural
{
    [Key] public long CalculationId { get; set; }
    [ForeignKey(nameof(CalculationId))] public Calculation? Calculation { get; set; }
    public int? DamTypeId { get; set; }
    [ForeignKey(nameof(DamTypeId))] public DamType? DamType { get; set; }
    public decimal? DamLengthM { get; set; }
    public decimal? DamMaxHeightM { get; set; }
    public decimal? CrestWidthM { get; set; }
    public int? SpillwaysCount { get; set; }
    public int? SpillwayTypeId { get; set; }
    [ForeignKey(nameof(SpillwayTypeId))] public SpillwayType? SpillwayType { get; set; }
    public int? TurbineTypeId { get; set; }
    [ForeignKey(nameof(TurbineTypeId))] public TurbineType? TurbineType { get; set; }
    public int? UnitsCount { get; set; }
    public decimal? InstalledPowerMw { get; set; }
}

[Table("calc_initial")]
public class CalcInitial
{
    [Key] public long CalculationId { get; set; }
    [ForeignKey(nameof(CalculationId))] public Calculation? Calculation { get; set; }
    public decimal? NormalHeadwaterM { get; set; }
    public decimal? DeadVolumeLevelM { get; set; }
    public decimal? ReservoirFullVolumeMlnM3 { get; set; }
    public decimal? ReservoirUsefulVolumeMlnM3 { get; set; }
    public decimal? RoughnessCoeffN { get; set; }
    public decimal? WaterTemperatureC { get; set; }
}

[Table("calc_operation_modes")]
public class CalcOperationMode
{
    [Key] public long Id { get; set; }
    public long CalculationId { get; set; }
    [ForeignKey(nameof(CalculationId))] public Calculation? Calculation { get; set; }
    [MaxLength(50)] public string Period { get; set; } = null!;
    [MaxLength(100)] public string PeriodName { get; set; } = null!;
    public decimal? AvgFlowM3s { get; set; }
    public decimal? MaxFlowM3s { get; set; }
    public decimal? MinFlowM3s { get; set; }
    public decimal? UpstreamLevelM { get; set; }
    public decimal? DownstreamLevelM { get; set; }
}

[Table("calc_flow_provision")]
public class CalcFlowProvision
{
    [Key] public long Id { get; set; }
    public long CalculationId { get; set; }
    [ForeignKey(nameof(CalculationId))] public Calculation? Calculation { get; set; }
    public decimal ProvisionPct { get; set; }
    public decimal FlowM3s { get; set; }
}

[Table("calc_water_balance")]
public class CalcWaterBalance
{
    [Key] public long Id { get; set; }
    public long CalculationId { get; set; }
    [ForeignKey(nameof(CalculationId))] public Calculation? Calculation { get; set; }
    [MaxLength(100)] public string UseCategory { get; set; } = null!;
    public decimal SharePct { get; set; }
}

[Table("calc_power_generation")]
public class CalcPowerGeneration
{
    [Key] public long Id { get; set; }
    public long CalculationId { get; set; }
    [ForeignKey(nameof(CalculationId))] public Calculation? Calculation { get; set; }
    public short MonthNum { get; set; }
    public decimal GenerationMkwh { get; set; }
}

[Table("calc_media")]
public class CalcMedia
{
    [Key] public long Id { get; set; }
    public long CalculationId { get; set; }
    [ForeignKey(nameof(CalculationId))] public Calculation? Calculation { get; set; }
    [MaxLength(30)] public string MediaType { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public string? Caption { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
