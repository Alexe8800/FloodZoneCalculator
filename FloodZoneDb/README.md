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

## Импорт исходных изодат

Изодаты из листов `Левый берег` и `Правый берег` сохраняются отдельно в `isodats` и не
связываются с `cross_sections` или `bank_points`. Импорт хранит тип изодаты, исходные
значение и `Ri`, лист, строку/колонки Excel, имя файла и его SHA-256. Таблица создаётся
идемпотентно импортёром; повторная загрузка идентичного файла не дублирует исходные ячейки.

Пример вызова из отдельного importer/CLI:

```csharp
var repository = IsodatPostgresRepository.FromEnvironment();
var importer = new IsodatExcelImportService(repository);
var result = await importer.ImportAsync(excelPath);
```

Импортёр читает книгу, отображает её поля на `IsodatRecord` и сохраняет записи одной
транзакцией. Он не запускает гидравлические расчёты и не изменяет расчётные таблицы,
створы или точки профиля.
