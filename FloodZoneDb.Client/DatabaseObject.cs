namespace FloodZoneDb.Client;

public sealed class DatabaseObject
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string TypeCode { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }

    public override string ToString() => Name;
}
