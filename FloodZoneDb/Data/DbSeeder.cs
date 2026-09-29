using FloodZoneDb.Domain;
using Microsoft.EntityFrameworkCore;

namespace FloodZoneDb.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        if (!await db.ObjectTypes.AnyAsync(cancellationToken))
        {
            db.ObjectTypes.AddRange(
                new ObjectType { Code = ObjectTypeCodes.HydroNode, Name = "Гидроузел" },
                new ObjectType { Code = ObjectTypeCodes.Dam, Name = "Плотина" },
                new ObjectType { Code = ObjectTypeCodes.Spillway, Name = "Водосброс" },
                new ObjectType { Code = ObjectTypeCodes.Building, Name = "Здание" },
                new ObjectType { Code = ObjectTypeCodes.Equipment, Name = "Оборудование" });
        }

        if (!await db.Statuses.AnyAsync(cancellationToken))
            db.Statuses.AddRange(
                new Status { Code = "active", Name = "Действует" },
                new Status { Code = "under_construction", Name = "Строится" },
                new Status { Code = "decommissioned", Name = "Выведен из эксплуатации" });

        if (!await db.EquipmentTypes.AnyAsync(cancellationToken))
            db.EquipmentTypes.AddRange(
                new EquipmentType { Code = "turbine", Name = "Турбина" },
                new EquipmentType { Code = "generator", Name = "Генератор" },
                new EquipmentType { Code = "transformer", Name = "Трансформатор" });

        await db.SaveChangesAsync(cancellationToken);
    }
}
