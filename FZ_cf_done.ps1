$p = "FloodZoneCalculator\Presentation\ConnectionForm.cs"
$f = Get-Content $p -Encoding UTF8
Write-Host "lines before: $($f.Length)"

# H: вставить TestConnectionAsync после строки 88 (index 88 = "        }")
$testMethod = @"
        private async Task TestConnectionAsync()
        {
            try
            {
                _testButton.Enabled = false;
                _status.Text = "Проверка подключения к БД...";
                var database = new FloodZoneConnection(_connectionText.Text);
                var result = await database.CanConnectAsync();
                if (result)
                {
                    _status.Text = "Подключение к БД успешно.";
                    AppLogger.Info("Тест подключения к БД успешен.");
                    MessageBox.Show(this, "Подключение к PostgreSQL успешно.", "Тест БД", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _status.Text = "База данных недоступна. Доступен оффлайн-режим.";
                    AppLogger.Info("Тест подключения к БД неудачен, доступна оффлайн-работа.");
                    MessageBox.Show(this, "База данных недоступна. Доступен оффлайн-режим: создание объектов и расчёты без сохранения.", "Тест БД", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка теста подключения.", ex);
                _status.Text = "Ошибка: " + ex.Message;
                MessageBox.Show(this, ex.Message, "Тест БД", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _testButton.Enabled = true;
            }
        }
"@
$f = $f[0..88] + $testMethod.TrimEnd("`n") + $f[89..($f.Length-1)]
Write-Host "after H: $($f.Length)"

# G: isOffline: false
$f[72] = $f[72] -replace 'new ObjectBrowserForm\(database\)', 'new ObjectBrowserForm(database, isOffline: false)'
Write-Host "after G: $($f.Length)"

# F: оффлайн-переход в ConnectAsync (индексы 65-68 -> 10 строк)
$newF = @(
"                if (!await database.CanConnectAsync())",
"                {",
"                    AppLogger.Info(""Подключение к БД недоступно — переходим в оффлайн-режим."");",
"                    Hide();",
"                    using (var objects = new ObjectBrowserForm(database, isOffline: true))",
"                        objects.ShowDialog(this);",
"                    Show();",
"                    return;",
"                }",
"                _status.Text = ""Подключение и применение миграции..."";" ,
"                await database.EnsureDatabaseAndSchemaAsync();"
)
$f = $f[0..64] + $newF + $f[69..($f.Length-1)]
Write-Host "after F: $($f.Length)"

# E: статус на строку 4
$f[55] = $f[55] -replace 'Controls.Add\(_status, 1, 3\)', 'Controls.Add(_status, 1, 4)'

# D: connectButton на строку 3
$f[53] = $f[53] -replace 'Controls.Add\(_connectButton, 1, 2\)', 'Controls.Add(_connectButton, 1, 3)'

# C: вставить кнопку _testButton перед _connectButton (index 51)
$newC = @(
"            _testButton = new Button { Text = ""Тест БД"", AutoSize = true, Height = 34 };",
"            _testButton.Click += async (s, e) => await TestConnectionAsync();",
"            layout.Controls.Add(_testButton, 1, 2);"
)
$f = $f[0..50] + $newC + $f[51..($f.Length-1)]
Write-Host "after C: $($f.Length)"

# B: RowCount
$f[34] = $f[34] -replace 'RowCount = 4', 'RowCount = 5'

# A: поле _testButton после _connectButton (index 13)
$newA = "        private Button _testButton = null!;"
$f = $f[0..13] + $newA + $f[14..($f.Length-1)]
Write-Host "after A: $($f.Length)"

Set-Content -Path $p -Value ($f -join "`n") -Encoding UTF8
Write-Host "written"
