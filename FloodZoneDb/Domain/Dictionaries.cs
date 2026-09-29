using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FloodZoneDb.Domain;

[Table("object_types")]
public class ObjectType
{
    [Key] public int Id { get; set; }
    [MaxLength(50)] public string Code { get; set; } = null!;
    [MaxLength(200)] public string Name { get; set; } = null!;
    [MaxLength(50)] public string? ParentCode { get; set; }
    [MaxLength(255)] public string? Icon { get; set; }
}

[Table("rivers")]
public class River
{
    [Key] public int Id { get; set; }
    [MaxLength(200)] public string Name { get; set; } = null!;
    public decimal? LengthKm { get; set; }
    public decimal? BasinAreaKm2 { get; set; }
}

[Table("subjects_rf")]
public class SubjectRf
{
    [Key] public int Id { get; set; }
    [MaxLength(200)] public string Name { get; set; } = null!;
    [MaxLength(10)] public string? Code { get; set; }
}

[Table("organizations")]
public class Organization
{
    [Key] public int Id { get; set; }
    [MaxLength(300)] public string Name { get; set; } = null!;
    [MaxLength(12)] public string? Inn { get; set; }
    [MaxLength(15)] public string? Ogrn { get; set; }
    [MaxLength(50)] public string? Role { get; set; }
}

[Table("users")]
public class User
{
    [Key] public int Id { get; set; }
    [MaxLength(100)] public string Login { get; set; } = null!;
    [MaxLength(200)] public string? FullName { get; set; }
    [MaxLength(50)] public string Role { get; set; } = "viewer";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("statuses")]
public class Status
{
    [Key] public int Id { get; set; }
    [MaxLength(50)] public string Code { get; set; } = null!;
    [MaxLength(200)] public string Name { get; set; } = null!;
}

[Table("dam_types")]
public class DamType
{
    [Key] public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = null!;
}

[Table("spillway_types")]
public class SpillwayType
{
    [Key] public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = null!;
}

[Table("turbine_types")]
public class TurbineType
{
    [Key] public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = null!;
}

[Table("generator_types")]
public class GeneratorType
{
    [Key] public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = null!;
}

[Table("equipment_types")]
public class EquipmentType
{
    [Key] public int Id { get; set; }
    [MaxLength(50)] public string Code { get; set; } = null!;
    [MaxLength(200)] public string Name { get; set; } = null!;
}
