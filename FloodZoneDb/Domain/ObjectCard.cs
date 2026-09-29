using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FloodZoneDb.Domain;

[Table("object_technical_specs")]
public class ObjectTechnicalSpec
{
    [Key] public long Id { get; set; }
    public long ObjectId { get; set; }
    [ForeignKey(nameof(ObjectId))] public GtsObject? Object { get; set; }
    [MaxLength(100)] public string SpecKey { get; set; } = null!;
    public string? SpecValue { get; set; }
    [MaxLength(30)] public string? Unit { get; set; }
}

[Table("object_condition")]
public class ObjectCondition
{
    [Key] public long ObjectId { get; set; }
    [ForeignKey(nameof(ObjectId))] public GtsObject? Object { get; set; }
    public decimal? PhysicalWearPct { get; set; }
    [MaxLength(100)] public string? TechCondition { get; set; }
    public DateOnly? LastInspectionAt { get; set; }
    public DateOnly? NextInspectionAt { get; set; }
    [MaxLength(100)] public string? AccidentRate { get; set; }
    public string? SpecialMarks { get; set; }
}

[Table("object_location")]
public class ObjectLocation
{
    [Key] public long ObjectId { get; set; }
    [ForeignKey(nameof(ObjectId))] public GtsObject? Object { get; set; }
    public int? SubjectRfId { get; set; }
    [ForeignKey(nameof(SubjectRfId))] public SubjectRf? SubjectRf { get; set; }
    [MaxLength(200)] public string? District { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Address { get; set; }
}

[Table("object_media")]
public class ObjectMedia
{
    [Key] public long Id { get; set; }
    public long ObjectId { get; set; }
    [ForeignKey(nameof(ObjectId))] public GtsObject? Object { get; set; }
    [MaxLength(20)] public string MediaType { get; set; } = null!;
    public string FilePath { get; set; } = null!;
    public string? Caption { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

[Table("object_notes")]
public class ObjectNote
{
    [Key] public long ObjectId { get; set; }
    [ForeignKey(nameof(ObjectId))] public GtsObject? Object { get; set; }
    public string? ShortDescription { get; set; }
    public string? Remarks { get; set; }
}

[Table("object_quick_info")]
public class ObjectQuickInfo
{
    [Key] public long ObjectId { get; set; }
    [ForeignKey(nameof(ObjectId))] public GtsObject? Object { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
    [ForeignKey(nameof(UpdatedByUserId))] public User? UpdatedBy { get; set; }
}
