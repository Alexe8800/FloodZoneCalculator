using System.Collections.Generic;

namespace FloodZoneDb.Client;

public sealed class ObjectProfile
{
    public string River { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string CommissioningDate { get; set; } = "";
    public string BalanceAffiliation { get; set; } = "";
    public string ResponsiblePerson { get; set; } = "";
    public string Status { get; set; } = "";
    public string GtsClass { get; set; } = "";
    public string ReliabilityCategory { get; set; } = "";
    public string DesignOrganization { get; set; } = "";
    public string ProjectCode { get; set; } = "";
    public string DesignPowerMw { get; set; } = "";
    public string UnitsCount { get; set; } = "";
    public string AverageAnnualGenerationMkwh { get; set; } = "";
    public string EfficiencyPct { get; set; } = "";
    public string ReliabilityCoefficient { get; set; } = "";
    public string DamType { get; set; } = "";
    public string DamLengthM { get; set; } = "";
    public string DamMaxHeightM { get; set; } = "";
    public string NormalHeadwaterM { get; set; } = "";
    public string MinWaterLevelM { get; set; } = "";
    public string ReservoirVolumeMlnM3 { get; set; } = "";
    public string SpillwayType { get; set; } = "";
    public string SpillwayCapacityM3s { get; set; } = "";
    public string SpillwayOpeningsCount { get; set; } = "";
    public string TurbineType { get; set; } = "";
    public string GeneratorType { get; set; } = "";
    public string VoltageKv { get; set; } = "";
    public string PhysicalWearPct { get; set; } = "";
    public string TechnicalCondition { get; set; } = "";
    public string LastInspectionDate { get; set; } = "";
    public string NextInspectionDate { get; set; } = "";
    public string AccidentRate { get; set; } = "";
    public string SpecialMarks { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string Remarks { get; set; } = "";
    public string SubjectRf { get; set; } = "";
    public string District { get; set; } = "";
    public string Latitude { get; set; } = "";
    public string Longitude { get; set; } = "";
    public string Address { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public string ImageBase64 { get; set; } = "";
    public string SchemeImagePath { get; set; } = "";
    public string SchemeImageBase64 { get; set; } = "";
    public string HydraulicProfileImagePath { get; set; } = "";
    public string HydraulicProfileImageBase64 { get; set; } = "";
    public string CatchmentAreaKm2 { get; set; } = "";
    public string AverageAnnualRunoffKm3 { get; set; } = "";
    public string MaxFlowM3s { get; set; } = "";
    public string MinFlowM3s { get; set; } = "";
    public string ProvisionPct { get; set; } = "";
    public string DeadVolumeLevelM { get; set; } = "";
    public string ReservoirUsefulVolumeMlnM3 { get; set; } = "";
    public string RoughnessCoeffN { get; set; } = "";
    public string WaterTemperatureC { get; set; } = "";
    public string BalanceOwnership { get; set; } = "";
    public string HydroClass { get; set; } = "";
    public string ProjectNumber { get; set; } = "";
    public CalculationInput CalculationInputs { get; set; } = new();

    // Эксплуатационные характеристики по периодам и помесячная выработка (расход/напор/выработка).
    // Хранятся внутри profile_json (jsonb), поэтому отдельная миграция БД не требуется.
    public List<OperationPeriodEntry> OperationPeriods { get; set; } = new();
    public List<MonthlyGenerationEntry> MonthlyGeneration { get; set; } = new();
}

// Одна строка таблицы эксплуатационных характеристик (значения как в grid, форматирование на UI).
public sealed class OperationPeriodEntry
{
    public string Period { get; set; } = "";
    public string QAvg { get; set; } = "";
    public string QMax { get; set; } = "";
    public string QMin { get; set; } = "";
    public string Hwl { get; set; } = "";
    public string Lwl { get; set; } = "";
}

// Одна строка помесячной выработки: месяц 1..12 + расход/напор/выработка.
public sealed class MonthlyGenerationEntry
{
    public int Month { get; set; }
    public string Flow { get; set; } = "";
    public string Head { get; set; } = "";
    public string Generation { get; set; } = "";
}
