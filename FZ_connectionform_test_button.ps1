$p = "FloodZoneCalculator\Presentation\ConnectionForm.cs"
$f = Get-Content $p -Encoding UTF8
$f = $f -join "`n"
$n=0

$oldA = "        private readonly TextBox _connectionText;
        private readonly Label _status;
        private Button _connectButton = null!;"
$newA = "        private readonly TextBox _connectionText;
        private readonly Label _status;
        private Button _connectButton = null!;
        private Button _testButton = null!;"
$f = $f -replace $oldA, $newA; $n++

$f = $f -replace 'RowCount = 4', 'RowCount = 5'; $n++

$oldC = "            _connectButton = new Button { Text = ""Подключиться и открыть объекты"", AutoSize = true, Height = 34 };
            _connectButton.Click += async (s, e) => await ConnectAsync();
            layout.Controls.Add(_connectButton, 1, 2);
            _status = new Label { Text = ""Введите строку подключения."", Dock = DockStyle.Fill, ForeColor = Color.DimGray };
            layout.Controls.Add(_status, 1, 3);"
$newC = "            _testButton = new Button { Text = ""Тест БД"", AutoSize = true, Height = 34 };
            _testButton.Click += async (s, e) => await TestConnectionAsync();
            layout.Controls.Add(_testButton, 1, 2);
            _connectButton = new Button { Text = ""Подключиться и открыть объекты"", AutoSize = true, Height = 34 };
            _connectButton.Click += async (s, e) => await ConnectAsync();
            layout.Controls.Add(_connectButton, 1, 3);
            _status = new Label { Text = ""Введите строку подключения."", Dock = DockStyle.Fill, ForeColor = Color.DimGray };
            layout.Controls.Add(_status, 1, 4);"
$f = $f -replace $oldC, $newC; $n++

$oldD = "        private async Task ConnectAsync()
        {
            try
            {
                _connectButton.Enabled = false;
                _status.Text = ""Подключение и применение миграции..."";
                var database = new FloodZoneConnection(_connectionText.Text);
                await database.EnsureDatabaseAndSchemaAsync();
                if (!await database.CanConnectAsync())
                    throw new InvalidOperationException(""PostgreSQL не подтвердил подключение."");
                AppLogger.Info(""Подключение к БД и миграция завершены."");
                Hide();
                using (var objects = new ObjectBrowserForm(database))
                    objects.ShowDialog(this);
                Show();
            }"
$newD = "        private async Task ConnectAsync()
        {
            try
            {
                _connectButton.Enabled = false;
                _status.Text = ""Проверка подключения..."";
                var database = new FloodZoneConnection(_connectionText.Text);
                if (!await database.CanConnectAsync())
                {
                    AppLogger.Info(""Подключение к БД недоступно — переходим в оффлайн-режим."");
                    Hide();
                    using (var objects = new ObjectBrowserForm(database, isOffline: true))
                        objects.ShowDialog(this);
                    Show();
                    return;
                }
                _status.Text = ""Подключение и применение миграции..."";
                await database.EnsureDatabaseAndSchemaAsync();
                AppLogger.Info(""Подключение к БД и миграция завершены."");
                Hide();
                using (var objects = new ObjectBrowserForm(database, isOffline: false))
                    objects.ShowDialog(this);
                Show();
            }"
$f = $f -replace $oldD, $newD; $n++

$marker = "        }
    }
}"
$newE = "
        private async Task TestConnectionAsync()
        {
            try
            {
                _testButton.Enabled = false;
                _status.Text = ""Проверка подключения к БД..."";
                var database = new FloodZoneConnection(_connectionText.Text);
                var result = await database.CanConnectAsync();
                if (result)
                {
                    _status.Text = ""Подключение к БД успешно."";
                    AppLogger.Info(""Тест подключения к БД успешен."");
                    MessageBox.Show(this, ""Подключение к PostgreSQL успешно."", ""Тест БД"", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _status.Text = ""База данных недоступна. Доступен оффлайн-режим."";
                    AppLogger.Info(""Тест подключения к БД неудачен, доступна оффлайн-работа."");
                    MessageBox.Show(this, ""База данных недоступна. Доступен оффлайн-режим: создание объектов и расчёты без сохранения."", ""Тест БД"", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error(""Ошибка теста подключения."", ex);
                _status.Text = ""Ошибка: "" + ex.Message;
                MessageBox.Show(this, ex.Message, ""Тест БД"", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _testButton.Enabled = true;
            }
        }"
$f = $f -replace $marker, ($newE + "`n" + $marker); $n++

Write-Host "edits applied: $n"
[System.IO.File]::WriteAllText($p, $f, [System.Text.UTF8Encoding]::new($false))
Write-Host "written"
