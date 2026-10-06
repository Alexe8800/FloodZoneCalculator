$p = "FloodZoneCalculator\Presentation\ConnectionForm.cs"
$f = Get-Content $p -Encoding UTF8
Write-Host "lines before: $($f.Length)"

# 1: удалить дубликат _testButton (строка 16 = index 15)
$f = $f[0..14] + $f[16..($f.Length-1)]
Write-Host "after dedup: $($f.Length)"

# найти индексы
$testBtn = 0; for ($i=0; $i -lt $f.Length; $i++) { if ($f[$i] -like "*Тест БД*") { $testBtn=$i; break } }
$connectBtn = 0; for ($i=0; $i -lt $f.Length; $i++) { if ($f[$i] -like "*Подключиться и открыть объекты*") { $connectBtn=$i; break } }
$status = 0; for ($i=0; $i -lt $f.Length; $i++) { if ($f[$i] -like "*Введите строку подключения*") { $status=$i; break } }
Write-Host "testBtn=$testBtn connectBtn=$connectBtn status=$status"

# 2: вставить _testButton кнопку перед _connectButton
$testButtonLines = @(
"            _testButton = new Button { Text = ""Тест БД"", AutoSize = true, Height = 34 };",
"            _testButton.Click += async (s, e) => await TestConnectionAsync();",
"            layout.Controls.Add(_testButton, 1, 2);"
)
$f = $f[0..($connectBtn-1)] + $testButtonLines + $f[$connectBtn..($f.Length-1)]
Write-Host "after test button insert: $($f.Length)"

# 3: сдвинуть _connectButton и _status на одну строку вниз
$f = $f[0..($connectBtn+3)] + @(
"            layout.Controls.Add(_connectButton, 1, 3);"
) + $f[($connectBtn+4)..($f.Length-1)]
Write-Host "after connect button move: $($f.Length)"

# 4: статус на строку 4
$f = $f -replace 'layout\.Controls\.Add\(_status, 1, 3\);', "layout.Controls.Add(_status, 1, 4);"
Write-Host "after status move: $($f.Length)"

# 5: ConnectAsync оффлайн-переход
$f = $f -replace '"Подключение и применение миграции...";', '"Проверка подключения...";' -replace 'await database\.EnsureDatabaseAndSchemaAsync\(\);', `
"                if (!await database.CanConnectAsync())
                {
                    AppLogger.Info(""Подключение к БД недоступно — переходим в оффлайн-режим."");
                    Hide();
                    using (var objects = new ObjectBrowserForm(database, isOffline: true))
                        objects.ShowDialog(this);
                    Show();
                    return;
                }
                _status.Text = ""Подключение и применение миграции..."";
                await database.EnsureDatabaseAndSchemaAsync();"
-freplace 'using \(var objects = new ObjectBrowserForm\(database\)\)', 'using (var objects = new ObjectBrowserForm(database, isOffline: false))'
Write-Host "after ConnectAsync edit: $($f.Length)"

$f = $f -join "`n"
[System.IO.File]::WriteAllText($p, $f, [System.Text.UTF8Encoding]::new($false))
Write-Host "written";
