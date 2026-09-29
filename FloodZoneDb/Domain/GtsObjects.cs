using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FloodZoneDb.Domain;

[Table("objects")]
public class GtsObject
{
    [Key] public long Id { get; set; }
    public long? ParentId { get; set; }
    public int ObjectTypeId { get; set; }
    [ForeignKey(nameof(ObjectTypeId))] public ObjectType? ObjectType { get; set; }
    [MaxLength(300)] public string Name { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public GtsObject? Parent { get; set; }
    public ICollection<GtsObject> Children { get; set; } = new List<GtsObject>();
}

[Table("hydro_nodes")]
public class HydroNode
{
    [Key] public long ObjectId { get; set; }
    [ForeignKey(nameof(ObjectId))] public GtsObject Object { get; set; } = null!;
    public int? RiverId { get; set; }
    [ForeignKey(nameof(RiverId))] public River? River { get; set; }
    public string? Purpose { get; set; }
    public DateOnly? CommissioningDate { get; set; }
    public int? OwnerOrgId { get; set; }
    [ForeignKey(nameof(OwnerOrgId))] public Organization? OwnerOrg { get; set; }
    [MaxLength(200)] public string? BalanceAffiliation { get; set; }
    [MaxLength(200)] public string? ResponsiblePerson { get; set; }
    public int? StatusId { get; set; }
    [ForeignKey(nameof(StatusId))] public Status? Status { get; set; }
    public ObjectClass? GtsClass { get; set; }
    public ReliabilityCategory? ReliabilityCategory { get; set; }
    public int? DesignOrgId { get; set; }
    [ForeignKey(nameof(DesignOrgId))] public Organization? DesignOrg { get; set; }
    [MaxLength(50)] public string? ProjectCode { get; set; }
    public decimal? DesignPowerMw { get; set; }
    public int? UnitsCount { get; set; }
    public decimal? AvgAnnualGenerationMkwh { get; set; }
}

[Table("dams")]
public class Dam
{
    [Key] public long ObjectId { get; set; }
    [ForeignKey(nameof(ObjectId))] public GtsObject Object { get; set; } = null!;
    public int? DamTypeId { get; set; }
    [ForeignKey(nameof(DamTypeId))] public DamType? DamType { get; set; }
    public decimal? LengthM { get; set; }
    public decimal? MaxHeightM { get; set; }
    public decimal? NormalHeadwaterM { get; set; }
    public decimal? MinWaterLevelM { get; set; }
    public decimal? ReservoirVolumeMlnM3 { get; set; }
}

[Table("spillways")]
public class Spillway
{
    [Key] public long ObjectId { get; set; }
    [ForeignKey(nameof(ObjectId))] public GtsObject Object { get; set; } = null!;
    public int? SpillwayTypeId { get; set; }
    [ForeignKey(nameof(SpillwayTypeId))] public SpillwayType? SpillwayType { get; set; }
    public decimal? MaxCapacityM3s { get; set; }
    public int? OpeningsCount { get; set; }
}

[Table("buildings")]
public class Building
{
    [Key] public long ObjectId { get; set; }
    [ForeignKey(nameof(ObjectId))] public GtsObject Object { get; set; } = null!;
    public decimal? AreaM2 { get; set; }
    public decimal? HeightM { get; set; }
    public string? Purpose { get; set; }
}

[Table("equipment")]
public class Equipment
{
    [Key] public long ObjectId { get; set; }
    [ForeignKey(nameof(ObjectId))] public GtsObject Object { get; set; } = null!;
    public int? EquipmentTypeId { get; set; }
    [ForeignKey(nameof(EquipmentTypeId))] public EquipmentType? EquipmentType { get; set; }
    public int? TurbineTypeId { get; set; }
    [ForeignKey(nameof(TurbineTypeId))] public TurbineType? TurbineType { get; set; }
    public int? GeneratorTypeId { get; set; }
    [ForeignKey(nameof(GeneratorTypeId))] public GeneratorType? GeneratorType { get; set; }
    public decimal? VoltageKv { get; set; }
    public decimal? PowerMw { get; set; }
    [MaxLength(100)] public string? SerialNumber { get; set; }
    public DateOnly? InstalledAt { get; set; }
}
