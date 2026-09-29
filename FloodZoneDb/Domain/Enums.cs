using NpgsqlTypes;

namespace FloodZoneDb.Domain;

[PgName("object_class")]
public enum ObjectClass
{
    [PgName("I")] ClassI = 1,
    [PgName("II")] ClassII = 2,
    [PgName("III")] ClassIII = 3,
    [PgName("IV")] ClassIV = 4
}

[PgName("reliability_category")]
public enum ReliabilityCategory
{
    [PgName("особо ответственное")] VeryCritical = 1,
    [PgName("ответственное")] Critical = 2,
    [PgName("пониженное")] Low = 3
}

[PgName("bank_side")]
public enum BankSide
{
    [PgName("L")] Left = 1,
    [PgName("P")] Right = 2
}

[PgName("calc_status")]
public enum CalcStatus
{
    [PgName("draft")] Draft = 1,
    [PgName("computed")] Computed = 2,
    [PgName("approved")] Approved = 3,
    [PgName("archived")] Archived = 4
}

public static class ObjectTypeCodes
{
    public const string HydroNode = "hydro_node";
    public const string Dam = "dam";
    public const string Spillway = "spillway";
    public const string Building = "building";
    public const string Equipment = "equipment";
}
