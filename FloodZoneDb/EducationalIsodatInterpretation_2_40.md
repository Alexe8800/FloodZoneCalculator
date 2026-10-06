# Этап 2.40 — синтетическая учебная интерпретация изодат

> На этапе 2.40 введена синтетическая учебная интерпретация данных Excel. Она не является восстановлением официальной методики.

## Принятая интерпретация

Для отдельной учебной модели приняты только следующие условные отображения:

| Поле исходной записи | Учебная модель | Ограничение |
|---|---|---|
| `IsodatRecord.DistanceM` (колонка исходной записи, обозначенная `Ri`) | `IsodatHydraulicPoint.X` | Учебное правило `Ri → X`; не утверждение о физическом значении `Ri` и не изменение реальной геометрии. |
| Запись типа `Depth`, `Value` | `IsodatHydraulicPoint.DepthH` | Учебное правило `Depth → h`; не связывает эти точки с `CrossSectionGeometry` или входом production-калькулятора. |
| Запись типа `Velocity`, `Value` | `IsodatHydraulicPoint.ObservedVelocity` | Учебное правило `Velocity → V_observed`; это отдельное наблюдаемое/исходное значение, не `V_calculated`. |
| `CalculatedFloodDuration` и `ActualFloodDuration` | Не переносятся в учебную гидравлическую точку | В этом этапе не используются и физически не интерпретируются. Их исходные записи остаются в `isodats`. |

У каждой точки фиксируется `Interpretation = "EducationalSynthetic"`. Конструктор модели не позволяет выбрать другой статус. В таблице статус также ограничен SQL `CHECK`.

## Новая модель и группировка

Добавлена отдельная immutable-модель [IsodatHydraulicPoint.cs](../FloodZoneDb.Client/IsodatHydraulicPoint.cs), содержащая:

- `Id`;
- `CrossSectionNumber`, `Side`;
- `X`;
- nullable `DepthH` либо nullable `ObservedVelocity` (для одной точки заполняется значение только её исходного типа);
- `SourceIsodatId`;
- `SourceFileSha256`, `SourceSheet`, `SourceRow`, `SourceValueColumn`;
- неизменяемый статус `EducationalSynthetic`.

Каждая Depth- или Velocity-запись преобразуется отдельно. Значения Depth и Velocity не объединяются по X. Значения X могут повторяться. Исходный порядок сохраняется в `Points`; модель дополнительно даёт сортированное представление `PointsSortedByX`, не меняя исходную последовательность. Для каждого профиля доступны `MinX`, `MaxX`, `MinDepth`, `MaxDepth`, `MinObservedVelocity`, `MaxObservedVelocity`. Если в группе отсутствует один тип значения, его статистика равна `null`.

Преобразование сделано в [IsodatHydraulicPointMapper.cs](../FloodZoneDb.Client/IsodatHydraulicPointMapper.cs); группировка по `CrossSectionNumber + Side` — в [IsodatHydraulicProfile.cs](../FloodZoneDb.Client/IsodatHydraulicProfile.cs).

## Импорт и хранение PostgreSQL

Путь реализации:

```text
PostgreSQL isodats
    → IsodatPostgresRepository.LoadAsync(sourceFileSha256)
    → IsodatHydraulicPointMapper (только Depth и Velocity)
    → IsodatHydraulicPointPostgresRepository
    → isodat_hydraulic_points
    → IsodatHydraulicProfileGrouper
```

Импортный сервис [IsodatHydraulicPointImportService.cs](../FloodZoneDb.Client/IsodatHydraulicPointImportService.cs) читает исходные данные через существующий repository. Он не перечитывает Excel. Идемпотентность обеспечена уникальностью `source_isodat_id` и `ON CONFLICT DO NOTHING`. В `isodat_hydraulic_points` внешний ключ ведёт только на исходную строку `isodats`; внешних ключей на `cross_sections` и `bank_points` нет. Уникальности X нет.

Добавленная SQL-схема: [CreateIsodatHydraulicPoints.sql](Migrations/CreateIsodatHydraulicPoints.sql), подключена как embedded resource в [FloodZoneDb.Client.csproj](../FloodZoneDb.Client/FloodZoneDb.Client.csproj). Исходный скрипт [CreateIsodats.sql](Migrations/CreateIsodats.sql) и таблица `isodats` не менялись.

## Результат по постоянному набору

PostgreSQL integration test прочитал из `isodats` все **360** записей книги по имени исходного файла. Из них:

- `Depth`: 48 строк (3 створа × 2 стороны × 8 записей);
- `Velocity`: 36 строк (3 створа × 2 стороны × 6 записей);
- всего создано/прочитано **84 учебные точки**;
- 276 записей временных типов не копируются и остаются в исходной таблице `isodats`;
- группировка даёт 6 профилей: три номера створов × две стороны, по 14 точек на профиль.

Повторный импорт добавил **0** строк. Снимок всей таблицы `isodats` (количество строк и hash содержимого) до и после импорта совпал. Учебные строки остаются в отдельной таблице для последующего этапа.

## Тесты и сборка

- Добавлены 7 unit-тестов для маппинга, сохранения идентификатора и метаданных, всех трёх створов и двух сторон, дублирующихся X, отсутствия интерполяции/дорисовки, неизменности входных записей и повторяемости группировки.
- Добавлен 1 PostgreSQL integration test для чтения существующих изодат, импорта, чтения 84 точек, проверки статистик и идемпотентности, включая hash-снимок `isodats`.
- Целевой прогон: **8 passed, 0 failed, 0 skipped**.
- Полный прогон решения: **316 passed, 0 failed, 0 skipped**.
- `dotnet build .\FloodZoneCalculator.sln --no-restore`: **0 ошибок, 17 предупреждений** (предупреждения проекта WinForms, в том числе nullable annotations/async в `MainForm.cs` и `DataEntryForm.cs`).
- PostgreSQL integration выполнялся с `FLOODZONE_CONN`, заданной только на время процесса теста; строка подключения в файлы не сохранялась.

## Неизменённые границы

- `CrossSectionGeometry` и `BankPoints` не менялись и не пополнялись точками из Excel.
- Учебная модель не объявляется официальной геометрией `CrossSection`.
- Исходная таблица `isodats` не изменялась; запись ведётся только в новую таблицу.
- Существующие calculators и production-цепочка `CrossSectionGeometry → omega → chi → R → vStar → C → V → Q` не изменялись и не вызываются этим импортом.
- Не вычисляются `omega`, `chi`, `R`, `vStar`, `C`, `V_calculated`, `Q`, `If`, время добегания или критерии ВЗМ/ВКЗМ/КР.
- `CalculatedFloodDuration` и `ActualFloodDuration` не интерпретируются физически.
- Никаких выводов об официальном смысле `Ri`, `Depth`, `Velocity` или crosswalk с геометрическими створами этот этап не делает: указанные отображения действуют исключительно как учебные синтетические правила.
