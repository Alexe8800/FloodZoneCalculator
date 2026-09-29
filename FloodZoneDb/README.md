# FloodZoneDb

Отдельный проект модели данных FloodZoneCalculator для PostgreSQL 14+.

## Требования

- .NET SDK 8.x
- PostgreSQL 14 или новее
- `FLOODZONE_CONN` — строка подключения к базе данных

Пример для PowerShell:

```powershell
$env:FLOODZONE_CONN = "Host=localhost;Port=5432;Database=floodzone;Username=postgres;Password=<локальный-пароль>"
dotnet run --project .\FloodZoneDb -- migrate
dotnet run --project .\FloodZoneDb -- seed
```

Проверка подключения:

```powershell
dotnet test .\FloodZoneDb.Tests
```

Тест реального соединения выполняется, когда задан `FLOODZONE_CONN`. Если переменная
не задана, тест пропускает сетевую проверку, а отдельный тест проверяет корректную
ошибку конфигурации.

Команда `--help` не подключается к PostgreSQL. Пароли не хранятся в репозитории.

## Миграции

После установки `dotnet-ef`:

```powershell
dotnet ef migrations add InitialCreate --project .\FloodZoneDb --startup-project .\FloodZoneDb
dotnet ef database update --project .\FloodZoneDb --startup-project .\FloodZoneDb
```

Модель использует snake_case, PostgreSQL enum-типы и shared-primary-key связи для подтипов
объектов и карточек. Таблицы результатов расчёта называются `calc_water_balance` и
`calc_power_generation`; эти имена единообразны в моделях, контексте и документации.
