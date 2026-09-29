using EFCore.NamingConventions;
using FloodZoneDb.Domain;
using Microsoft.EntityFrameworkCore;

namespace FloodZoneDb.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ObjectType> ObjectTypes => Set<ObjectType>();
    public DbSet<River> Rivers => Set<River>();
    public DbSet<SubjectRf> SubjectsRf => Set<SubjectRf>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Status> Statuses => Set<Status>();
    public DbSet<DamType> DamTypes => Set<DamType>();
    public DbSet<SpillwayType> SpillwayTypes => Set<SpillwayType>();
    public DbSet<TurbineType> TurbineTypes => Set<TurbineType>();
    public DbSet<GeneratorType> GeneratorTypes => Set<GeneratorType>();
    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();
    public DbSet<GtsObject> Objects => Set<GtsObject>();
    public DbSet<HydroNode> HydroNodes => Set<HydroNode>();
    public DbSet<Dam> Dams => Set<Dam>();
    public DbSet<Spillway> Spillways => Set<Spillway>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<ObjectTechnicalSpec> ObjectTechnicalSpecs => Set<ObjectTechnicalSpec>();
    public DbSet<ObjectCondition> ObjectConditions => Set<ObjectCondition>();
    public DbSet<ObjectLocation> ObjectLocations => Set<ObjectLocation>();
    public DbSet<ObjectMedia> ObjectMedia => Set<ObjectMedia>();
    public DbSet<ObjectNote> ObjectNotes => Set<ObjectNote>();
    public DbSet<ObjectQuickInfo> ObjectQuickInfo => Set<ObjectQuickInfo>();
    public DbSet<Calculation> Calculations => Set<Calculation>();
    public DbSet<CalcMainResult> CalcMainResults => Set<CalcMainResult>();
    public DbSet<CalcHydrological> CalcHydrologicals => Set<CalcHydrological>();
    public DbSet<CalcStructural> CalcStructurals => Set<CalcStructural>();
    public DbSet<CalcInitial> CalcInitials => Set<CalcInitial>();
    public DbSet<CalcOperationMode> CalcOperationModes => Set<CalcOperationMode>();
    public DbSet<CalcFlowProvision> CalcFlowProvisions => Set<CalcFlowProvision>();
    public DbSet<CalcWaterBalance> CalcWaterBalances => Set<CalcWaterBalance>();
    public DbSet<CalcPowerGeneration> CalcPowerGenerations => Set<CalcPowerGeneration>();
    public DbSet<CalcMedia> CalcMedia => Set<CalcMedia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<ObjectClass>(null, "object_class", null);
        modelBuilder.HasPostgresEnum<ReliabilityCategory>(null, "reliability_category", null);
        modelBuilder.HasPostgresEnum<BankSide>(null, "bank_side", null);
        modelBuilder.HasPostgresEnum<CalcStatus>(null, "calc_status", null);

        modelBuilder.Entity<ObjectType>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<River>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<SubjectRf>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<Organization>().HasIndex(x => x.Inn).IsUnique();
        modelBuilder.Entity<User>().HasIndex(x => x.Login).IsUnique();
        modelBuilder.Entity<Status>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<EquipmentType>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Calculation>().HasIndex(x => new { x.ObjectId, x.CalcNumber }).IsUnique();

        modelBuilder.Entity<GtsObject>()
            .HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        ConfigureSharedKey<HydroNode>(modelBuilder);
        ConfigureSharedKey<Dam>(modelBuilder);
        ConfigureSharedKey<Spillway>(modelBuilder);
        ConfigureSharedKey<Building>(modelBuilder);
        ConfigureSharedKey<Equipment>(modelBuilder);
        ConfigureSharedKey<ObjectCondition>(modelBuilder);
        ConfigureSharedKey<ObjectLocation>(modelBuilder);
        ConfigureSharedKey<ObjectNote>(modelBuilder);
        ConfigureSharedKey<ObjectQuickInfo>(modelBuilder);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties().Where(x => x.ClrType == typeof(decimal) || x.ClrType == typeof(decimal?)))
                property.SetColumnType("numeric");
        }

        base.OnModelCreating(modelBuilder);
    }

    private static void ConfigureSharedKey<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        modelBuilder.Entity<TEntity>()
            .HasKey("ObjectId");
    }
}
