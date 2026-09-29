using FloodZoneDb.Data;
using FloodZoneDb.Logging;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var command = args.FirstOrDefault()?.ToLowerInvariant();
if (command is "--help" or "-h" or null)
{
    Console.WriteLine("FloodZoneDb");
    Console.WriteLine("  migrate  Apply pending EF Core migrations.");
    Console.WriteLine("  seed     Apply migrations and seed reference data.");
    Console.WriteLine();
    Console.WriteLine("Set FLOODZONE_CONN to override the PostgreSQL connection string.");
    return;
}

if (command is not ("migrate" or "seed"))
{
    Console.Error.WriteLine($"Unknown command: {command}");
    Environment.ExitCode = 1;
    return;
}

var connectionString = Environment.GetEnvironmentVariable("FLOODZONE_CONN");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("FLOODZONE_CONN is not set. Refusing to connect with an implicit password.");
    Environment.ExitCode = 2;
    return;
}

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention()
    .Options;

try
{
    DatabaseCliLogger.Info("Запуск команды FloodZoneDb: " + command + ".");
    await EnsureDatabaseExistsAsync(connectionString);

    await using var db = new AppDbContext(options);
    switch (command)
    {
        case "migrate":
            await db.Database.MigrateAsync();
            Console.WriteLine("Migrations applied.");
            DatabaseCliLogger.Info("Миграции применены.");
            break;
        case "seed":
            await DbSeeder.SeedAsync(db);
            Console.WriteLine("Database migrated and reference data seeded.");
            DatabaseCliLogger.Info("Миграции и seed завершены.");
            break;
    }
}
catch (PostgresException ex) when (ex.SqlState == "28P01")
{
    DatabaseCliLogger.Error("PostgreSQL отклонил пароль.", ex);
    Console.Error.WriteLine(
        "PostgreSQL отклонил пароль. Проверьте Username/Password в FLOODZONE_CONN " +
        "или измените пароль пользователя postgres в pgAdmin.");
    Environment.ExitCode = 3;
}
catch (Exception ex)
{
    DatabaseCliLogger.Error("Ошибка выполнения команды FloodZoneDb.", ex);
    Console.Error.WriteLine("Ошибка записана в logs: " + ex.Message);
    Environment.ExitCode = 4;
}

static async Task EnsureDatabaseExistsAsync(string connectionString)
{
    var builder = new NpgsqlConnectionStringBuilder(connectionString);
    var databaseName = builder.Database;
    if (string.IsNullOrWhiteSpace(databaseName))
        throw new InvalidOperationException("В строке подключения не указано имя Database.");

    builder.Database = "postgres";
    await using var connection = new NpgsqlConnection(builder.ConnectionString);
    await connection.OpenAsync();

    await using var command = new NpgsqlCommand(
        "select 1 from pg_database where datname = @database_name;",
        connection);
    command.Parameters.AddWithValue("database_name", databaseName);
    var exists = await command.ExecuteScalarAsync();
    if (exists != null)
        return;

    var quotedName = "\"" + databaseName.Replace("\"", "\"\"") + "\"";
    await using var createCommand = new NpgsqlCommand("create database " + quotedName + ";", connection);
    await createCommand.ExecuteNonQueryAsync();
    Console.WriteLine($"База данных '{databaseName}' создана.");
}
