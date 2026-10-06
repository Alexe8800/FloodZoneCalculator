# Technical baseline — этап 2.39

Дата проверки: 2026-10-06.

## 1. Состояние production-кода

Технические слои существуют раздельно:

- Геометрия створов строится из `CrossSectionRecord.BankPoints` через `CrossSectionGeometryBuilder`. Используются сохранённые точки профиля; результаты содержат отдельные свойства геометрии и параметры записи створа.
- `CrossSectionGeometryCalculator` обходит сегменты геометрии и вычисляет площадь и длину смоченной части относительно явно переданного уровня воды. Промежуточные точки пересечения создаются только для результата вычисления.
- `HydraulicRadiusCalculator` получает два уже вычисленных значения и возвращает отношение площади к периметру.
- `ShearVelocityCalculator`, `GrishaninChezyCalculator`, `ChezyVelocityCalculator` и `DischargeCalculator` реализуют отдельные вычислительные шаги с валидацией входов.
- `CrossSectionHydraulicResultCalculator` связывает геометрический результат и явно переданные гидравлические входы в результат одной расчётной цепочки.
- `MultiCrossSectionHydraulicResultCalculator` строит геометрию для записей створов и применяет переданный `HydraulicCalculationInput`; последовательность упорядочивается по `DistanceFromHydroUnitM`.
- `HydraulicCalculationInput` хранит явные параметры `WaterLevel`, `Gravity`, `KinematicViscosity`, `DepthH`, `WidthB`, `HydraulicSlopeIf`.
- `CrossSectionHydraulicContext` объединяет `CrossSectionGeometry` с `HydraulicCalculationInput`.

Проверенные исходники: [CrossSectionGeometry.cs](../FloodZoneCalculator/Domain/CrossSectionGeometry.cs), [CrossSectionGeometryBuilder.cs](../FloodZoneCalculator/Domain/CrossSectionGeometryBuilder.cs), [CrossSectionGeometryCalculator.cs](../FloodZoneCalculator/Domain/CrossSectionGeometryCalculator.cs), [HydraulicRadiusCalculator.cs](../FloodZoneCalculator/Domain/HydraulicRadiusCalculator.cs), [GrishaninChezyCalculator.cs](../FloodZoneCalculator/Domain/GrishaninChezyCalculator.cs), [ShearVelocityCalculator.cs](../FloodZoneCalculator/Domain/ShearVelocityCalculator.cs), [ChezyVelocityCalculator.cs](../FloodZoneCalculator/Domain/ChezyVelocityCalculator.cs), [DischargeCalculator.cs](../FloodZoneCalculator/Domain/DischargeCalculator.cs), [CrossSectionHydraulicResultCalculator.cs](../FloodZoneCalculator/Domain/CrossSectionHydraulicResultCalculator.cs), [MultiCrossSectionHydraulicResultCalculator.cs](../FloodZoneCalculator/Domain/MultiCrossSectionHydraulicResultCalculator.cs), [HydraulicCalculationInput.cs](../FloodZoneCalculator/Domain/HydraulicCalculationInput.cs), [CrossSectionHydraulicContext.cs](../FloodZoneCalculator/Domain/CrossSectionHydraulicContext.cs).

Этот раздел фиксирует состояние и реализацию кода, а не методическую применимость формул к реальному объекту или изодатам.

## 2. Состояние реального Excel → PostgreSQL пути

Путь доступен в коде в следующем виде:

```text
Excel
→ IsodatExcelWorkbookReader
→ IsodatExcelRecordMapper
→ IsodatRecord
→ IsodatExcelImportService
→ PostgreSQL isodats
→ IsodatPostgresRepository.LoadAsync
→ IsodatGrouper
→ IsodatGrouping.GetSet
→ IsodatDataAuditor
```

- Reader читает структуру XLSX и значения ячеек.
- Mapper проверяет ожидаемые заголовки и строит записи с номером створа, стороной, типом, значением и исходными метаданными. Он отображает колонку `Ri_*` в свойство модели `DistanceM`; это фиксирует только кодовое присваивание.
- Import service предоставляет чтение и импорт. Импорт использует repository для сохранения записей в `isodats`.
- `LoadAsync` выполняет параметризованный `SELECT` из `isodats`, с необязательными фильтрами по имени/хешу файла, листу, номеру створа, стороне и типу.
- Grouper группирует уже загруженные записи; `GetSet` выбирает сторону для номера створа; auditor считает статистические агрегаты существующих записей.

Проверенные исходники: [IsodatExcelWorkbookReader.cs](../FloodZoneDb.Client/IsodatExcelWorkbookReader.cs), [IsodatExcelRecordMapper.cs](../FloodZoneDb.Client/IsodatExcelRecordMapper.cs), [IsodatRecord.cs](../FloodZoneDb.Client/IsodatRecord.cs), [IsodatExcelImportService.cs](../FloodZoneDb.Client/IsodatExcelImportService.cs), [IsodatPostgresRepository.cs](../FloodZoneDb.Client/IsodatPostgresRepository.cs), [CrossSectionIsodats.cs](../FloodZoneDb.Client/CrossSectionIsodats.cs), [IsodatDataAudit.cs](../FloodZoneDb.Client/IsodatDataAudit.cs).

## 3. Состояние геометрического пути

Путь геометрии начинается с `CrossSectionRecord` и содержащихся в нём `BankPoints`. `CrossSectionGeometryBuilder` преобразует точки в `ProfilePoint`, формирует упорядоченный профиль и сегменты, а также переносит значения полей записи створа в geometry-модель. Геометрические точки имеют собственные `Side`, `PointType`, `PointNumber`, `DistanceM` и `ElevationM`.

Изодатные записи не являются входом этого builder. Одноимённое поле `DistanceM` в моделях изодат и точки профиля не образует между ними связи.

## 4. Состояние новой гидравлической цепочки

Новая цепочка принимает геометрию и отдельный `HydraulicCalculationInput`. Внутри кода геометрический результат передаёт площадь и периметр следующим шагам; другие параметры берутся из явного входного объекта. Многосекционный калькулятор повторно использует переданный вход для обрабатываемых створов.

Наличие кода и прохождение синтетических тестов подтверждают только техническое поведение реализованной цепочки. Они не устанавливают источник фактических входных значений и не доказывают её применимость к реальным изодатам.

## 5. Границы между изодатами, геометрией и гидравлическими расчётами

| Подсистема | Её данные | Зафиксированная граница |
|---|---|---|
| Изодаты | `IsodatRecord`: номер Excel-створа, сторона, тип, `Value`, `DistanceM` и происхождение ячейки | Хранятся отдельно в `isodats`; grouping/audit работают с записями изодат. |
| Геометрия | `CrossSectionRecord` и `BankPoints`, преобразованные в `CrossSectionGeometry` | Builder принимает запись створа и точки профиля, не `IsodatRecord`. |
| Гидравлический расчёт | `CrossSectionGeometry` и `HydraulicCalculationInput` | Расчётный контекст принимает геометрию и отдельный объект входных параметров, не изодатную группировку. |

На текущей границе кода отсутствует подтверждованный переход:

```text
Isodat → CrossSectionGeometry
```

Также отсутствует подтверждованный переход:

```text
Isodat → HydraulicCalculationInput
```

Отдельно не считается установленным, что номера створов или похожие названия полей связывают эти подсистемы.

## 6. Тестовый baseline

Команда:

```powershell
dotnet test .\FloodZoneCalculator.sln --no-restore
```

Результат: **308 passed, 0 failed, 0 skipped**.

В полном прогоне действительно выполнились PostgreSQL integration tests для изодат: в `IsodatPostgresIntegrationTests` присутствуют четыре теста с атрибутом `PostgreSqlFact`. Успешный прогон включал проверку реального чтения/группировки постоянной книги и утверждения на 360 записей, а также сравнение снимков таблиц до/после read-only сценария.

Примечание: интеграционный класс содержит отдельный тест восстановления набора через существующий importer, если полный набор отсутствует. Поэтому успешное утверждение о 360 записях означает, что набор был доступен после выполнения этого теста; сам вывод стандартного runner не сообщает, требовалось ли восстановление. Интеграционные тесты также вызывают `EnsureSchemaAsync`; тестовый прогон не сохраняет отдельный schema diff. В рамках этапа schema-файлы не менялись, однако создание схемы вызовом теста не исключается отдельным сравнением метаданных PostgreSQL.

## 7. Build baseline

Команда:

```powershell
dotnet build .\FloodZoneCalculator.sln --no-restore
```

Результат: **0 ошибок, 0 предупреждений**.

## 8. Методически неизвестные связи

Этот baseline не разрешает и не предполагает:

- физический смысл `Ri`, является ли оно координатой или расстоянием;
- физическое тождество `Ri` и `DistanceM`;
- соответствие Excel `Depth` параметру `h`/`DepthH`;
- соответствие Excel `Velocity` рассчитываемой величине `V`;
- смысл `CalculatedFloodDuration` и `ActualFloodDuration`;
- соответствие Excel-номеров створов геометрическим `CrossSection`/`BankPoints`;
- происхождение и применимость `If`, `B`, `h`, `nu`, `g` и формулы `C`;
- методические критерии ВЗМ/ВКЗМ/КР и формулы для реальных изодат.

## 9. Что потребуется после получения первичной методики

Перед проектированием переходов необходимо сопоставить первичный материал с фактической структурой данных: получить определения и единицы полей, формулы и критерии, подтверждённый crosswalk Excel-створов с геометрией, а также контрольный расчёт с входными и промежуточными величинами. Только затем можно отдельно определить, существуют ли документально обоснованные переходы от изодат к геометрии или гидравлическим входам. Настоящий baseline такие переходы не создаёт.

## Объём изменений и рабочее дерево

В рамках этапа добавлен только этот Markdown-отчёт; production-код и файлы PostgreSQL schema не редактировались. Интеграционные тесты работали с БД согласно описанию выше. Перед началом этапа рабочее дерево уже содержало незакоммиченные изменения и новые файлы предыдущих этапов; они оставлены без изменений.

`FLOODZONE_CONN` был задан только в PowerShell-процессе запуска тестов и удалён из окружения этого процесса после завершения. Значение подключения в отчёт не включено.

> Техническая база готова к подключению методики, но методически подтверждённый расчёт реальных изодат не реализован.
