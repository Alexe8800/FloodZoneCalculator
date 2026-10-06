using FloodZoneCalculator.Infrastructure;
using FloodZoneDb.Client;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation
{
    public sealed class ConnectionForm : Form
    {
        private readonly TextBox _connectionText;
        private readonly Label _status;
        private Button _connectButton = null!;
        private Button _testButton = null!;

        public ConnectionForm()
        {
            Text = "FloodZoneCalculator — подключение";
            Width = 760;
            Height = 300;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);

            var header = new Label
            {
                Dock = DockStyle.Top, Height = 54,
                Text = "Подключение к базе данных",
                BackColor = Color.FromArgb(0, 82, 145), ForeColor = Color.White,
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                Padding = new Padding(16, 12, 0, 0)
            };
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(18),
                ColumnCount = 2, RowCount = 5
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
            layout.Controls.Add(new Label { Text = "Строка PostgreSQL:", Dock = DockStyle.Fill, Padding = new Padding(0, 7, 0, 0) }, 0, 0);
            _connectionText = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = Environment.GetEnvironmentVariable("FLOODZONE_CONN")
                    ?? "Host=localhost;Port=5433;Database=floodzone;Username=postgres;Password=1234"
            };
            layout.Controls.Add(_connectionText, 1, 0);
            layout.Controls.Add(new Label
            {
                Text = "Если базы ещё нет, она будет создана автоматически, затем применится миграция.",
                Dock = DockStyle.Fill, AutoSize = false, Padding = new Padding(0, 8, 0, 0)
            }, 1, 1);
            _testButton = new Button { Text = "Тест БД", AutoSize = true, Height = 34 };
            _testButton.Click += async (s, e) => await TestConnectionAsync();
            layout.Controls.Add(_testButton, 1, 2);
            _connectButton = new Button { Text = "Подключиться и открыть объекты", AutoSize = true, Height = 34 };
            _connectButton.Click += async (s, e) => await ConnectAsync();
            layout.Controls.Add(_connectButton, 1, 3);
            _status = new Label { Text = "Введите строку подключения.", Dock = DockStyle.Fill, ForeColor = Color.DimGray };
            layout.Controls.Add(_status, 1, 4);
            Controls.Add(layout);
            Controls.Add(header);
        }

        private async Task ConnectAsync()
        {
            try
            {
                _connectButton.Enabled = false;
                var database = new FloodZoneConnection(_connectionText.Text);
                if (!await database.CanConnectAsync())
                {
                    AppLogger.Info("Подключение к БД недоступно — переходим в оффлайн-режим.");
                    Hide();
                    using (var objects = new ObjectBrowserForm(database, isOffline: true))
                        objects.ShowDialog(this);
                    Show();
                    return;
                }
                _status.Text = "Подключение и применение миграции...";
                await database.EnsureDatabaseAndSchemaAsync();
                AppLogger.Info("Подключение к БД и миграция завершены.");
                Hide();
                using (var objects = new ObjectBrowserForm(database, isOffline: false))
                    objects.ShowDialog(this);
                Show();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ошибка подключения или миграции.", ex);
                _status.Text = "Ошибка: " + ex.Message;
                MessageBox.Show(this, ex.Message, "Подключение к БД",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _connectButton.Enabled = true;
            }
        }
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
    }
}
