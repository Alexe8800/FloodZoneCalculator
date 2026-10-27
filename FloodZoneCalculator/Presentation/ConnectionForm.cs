using FloodZoneCalculator.Infrastructure;
using FloodZoneDb.Client;
using Npgsql;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace FloodZoneCalculator.Presentation
{
    public sealed class ConnectionForm : Form
    {
        private readonly ModernTextBox _hostText = null!;
        private readonly ModernTextBox _portText = null!;
        private readonly ModernTextBox _databaseText = null!;
        private readonly ModernTextBox _userText = null!;
        private readonly ModernTextBox _passwordText = null!;
        private readonly Label _status;
        private ModernButton _connectButton = null!;
        private ModernButton _testButton = null!;

        public ConnectionForm()
        {
            ModernTheme.ApplyToForm(this);
            Text = "FloodZoneCalculator — подключение";
            Width = 560;
            Height = 520;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = ModernTheme.FontMain;

            var header = new ThemedPanel
            {
                Dock = DockStyle.Top, Height = 86,
                BackColor = ModernTheme.Background,
            };
            header.Paint += (s, e) =>
            {
                var g = e.Graphics;
                var rect = header.ClientRectangle;
                using var grad = ModernTheme.Gradient(rect, Color.FromArgb(8, 47, 73), ModernTheme.PrimaryDark,
                    LinearGradientOrientation.LeftToRight);
                g.FillRectangle(grad, rect);
                using var accent = new Pen(ModernTheme.PrimaryLight, 2f);
                g.DrawLine(accent, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
                using var titleBrush = new SolidBrush(Color.White);
                g.DrawString("FloodZoneCalculator", ModernTheme.FontHeading, titleBrush, 18, 12);
                using var subBrush = new SolidBrush(Color.FromArgb(190, 237, 248, 252));
                g.DrawString("Подключение к базе данных", ModernTheme.FontMain, subBrush, 20, 50);
            };

            var body = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ModernTheme.Background,
                Padding = new Padding(24, 16, 24, 12),
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2, RowCount = 8
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72));
            for (int i = 0; i < 8; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var settings = ReadInitialSettings();

            _hostText = AddRow(layout, 0, "Хост:", settings.Host);
            _portText = AddRow(layout, 1, "Порт:", settings.Port.ToString());
            _databaseText = AddRow(layout, 2, "База данных:", settings.Database);
            _userText = AddRow(layout, 3, "Пользователь:", settings.Username);

            layout.Controls.Add(new Label
            { Text = "Пароль:", Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0), ForeColor = ModernTheme.TextSub, BackColor = Color.Transparent }, 0, 4);
            _passwordText = new ModernTextBox
            {
                Dock = DockStyle.Fill,
                UseSystemPasswordChar = true,
                Text = settings.Password,
                Height = 34,
            };
            layout.Controls.Add(_passwordText, 1, 4);

            var note = new Label
            {
                Text = "Если базы ещё нет, она будет создана автоматически, затем применится миграция.",
                Dock = DockStyle.Fill, AutoSize = false, Height = 36,
                ForeColor = ModernTheme.TextMuted, BackColor = Color.Transparent,
                Font = ModernTheme.FontSmall,
                Padding = new Padding(0, 12, 0, 0)
            };
            layout.Controls.Add(note, 0, 5);
            layout.SetColumnSpan(note, 2);

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, Padding = new Padding(0, 10, 0, 0), BackColor = Color.Transparent
            };
            _testButton = ModernTheme.MakeButton("Тест БД", primary: false, height: 40);
            _testButton.AutoSize = true;
            _testButton.Click += async (s, e) => await TestConnectionAsync();
            _connectButton = ModernTheme.MakeButton("Подключиться и открыть объекты", primary: true, height: 40);
            _connectButton.AutoSize = true;
            _connectButton.Margin = new Padding(10, 0, 0, 0);
            _connectButton.Click += async (s, e) => await ConnectAsync();
            buttons.Controls.Add(_testButton);
            buttons.Controls.Add(_connectButton);
            layout.Controls.Add(buttons, 0, 6);
            layout.SetColumnSpan(buttons, 2);

            _status = new Label
            {
                Text = "Заполните параметры подключения.",
                Dock = DockStyle.Fill, AutoSize = false, Height = 40,
                ForeColor = ModernTheme.TextSub, BackColor = Color.Transparent,
                Font = ModernTheme.FontSmall,
                Padding = new Padding(0, 12, 0, 0)
            };
            layout.Controls.Add(_status, 0, 7);
            layout.SetColumnSpan(_status, 2);

            body.Controls.Add(layout);
            Controls.Add(body);
            Controls.Add(header);
        }

        private static ModernTextBox AddRow(TableLayoutPanel layout, int row, string caption, string value)
        {
            layout.Controls.Add(new Label
            {
                Text = caption, Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0),
                ForeColor = ModernTheme.TextSub, BackColor = Color.Transparent
            }, 0, row);
            var box = new ModernTextBox { Dock = DockStyle.Fill, Text = value, Height = 34 };
            layout.Controls.Add(box, 1, row);
            return box;
        }

        // Параметры по умолчанию берутся из переменной окружения FLOODZONE_CONN (если задана),
        // иначе — из стандартной локальной строки подключения.
        private static NpgsqlConnectionStringBuilder ReadInitialSettings()
        {
            var env = Environment.GetEnvironmentVariable("FLOODZONE_CONN");
            if (!string.IsNullOrWhiteSpace(env))
            {
                try { return new NpgsqlConnectionStringBuilder(env); }
                catch { /* падаем на значения по умолчанию */ }
            }
            return new NpgsqlConnectionStringBuilder(
                "Host=localhost;Port=5433;Database=floodzone;Username=postgres;Password=1234");
        }

        private string BuildConnectionString()
        {
            if (!int.TryParse(_portText.Text.Trim(), out var port) || port <= 0 || port > 65535)
                port = 5432;
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = _hostText.Text.Trim(),
                Port = port,
                Database = _databaseText.Text.Trim(),
                Username = _userText.Text.Trim(),
                Password = _passwordText.Text
            };
            return builder.ConnectionString;
        }

        private async Task ConnectAsync()
        {
            try
            {
                _connectButton.Enabled = false;
                var database = new FloodZoneConnection(BuildConnectionString());
                if (!await database.CanConnectToServerAsync())
                {
                    AppLogger.Info("Сервер PostgreSQL недоступен — переходим в оффлайн-режим.");
                    Hide();
                    using (var objects = new ObjectBrowserForm(database, isOffline: true))
                        objects.ShowDialog(this);
                    Show();
                    return;
                }
                _status.Text = "Подключение и применение миграции...";
                _status.ForeColor = ModernTheme.PrimaryLight;
                // Если базы нет — EnsureDatabaseAndSchemaAsync создаст её автоматически, затем применит миграцию.
                await database.EnsureDatabaseAndSchemaAsync();
                AppLogger.Info("Подключение к БД и миграция завершены.");
                Hide();
                using (var objects = new ObjectBrowserForm(database, isOffline: false))
                    objects.ShowDialog(this);
                Show();
            }
            catch (Exception ex)
            {
                var message = DescribeError(ex, _databaseText.Text.Trim());
                AppLogger.Error("Ошибка подключения или миграции.", ex);
                _status.Text = "Ошибка: " + message;
                _status.ForeColor = ModernTheme.Error;
                MessageBox.Show(this, message, "Подключение к БД",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _connectButton.Enabled = true;
                _status.ForeColor = ModernTheme.TextSub;
            }
        }

        private async Task TestConnectionAsync()
        {
            try
            {
                _testButton.Enabled = false;
                _status.Text = "Проверка подключения к БД...";
                _status.ForeColor = ModernTheme.PrimaryLight;
                var database = new FloodZoneConnection(BuildConnectionString());

                if (!await database.CanConnectToServerAsync())
                {
                    var offlineMessage = "Сервер PostgreSQL недоступен. Проверьте хост, порт и что сервер запущен. " +
                        "Доступен оффлайн-режим: создание объектов и расчёты без сохранения.";
                    _status.Text = "Сервер PostgreSQL недоступен. Доступен оффлайн-режим.";
                    _status.ForeColor = ModernTheme.Warning;
                    AppLogger.Info("Тест подключения: сервер PostgreSQL недоступен.");
                    MessageBox.Show(this, offlineMessage, "Тест БД",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Сервер доступен — проверяем целевую базу данных.
                try
                {
                    if (await database.CanConnectAsync())
                    {
                        _status.Text = "Подключение к БД успешно.";
                        _status.ForeColor = ModernTheme.Success;
                        AppLogger.Info("Тест подключения к БД успешен.");
                        MessageBox.Show(this, "Подключение к PostgreSQL успешно.", "Тест БД",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    var message = DescribeError(ex, _databaseText.Text.Trim());
                    _status.Text = message;
                    _status.ForeColor = ModernTheme.Warning;
                    AppLogger.Error("Тест подключения к БД: база недоступна.", ex);
                    MessageBox.Show(this, message, "Тест БД",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                var message = DescribeError(ex, _databaseText.Text.Trim());
                AppLogger.Error("Ошибка теста подключения.", ex);
                _status.Text = "Ошибка: " + message;
                _status.ForeColor = ModernTheme.Error;
                MessageBox.Show(this, message, "Тест БД",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _testButton.Enabled = true;
                if (_status.ForeColor != ModernTheme.Error && _status.ForeColor != ModernTheme.Success &&
                    _status.ForeColor != ModernTheme.Warning)
                    _status.ForeColor = ModernTheme.TextSub;
            }
        }

        // Преобразует ошибку PostgreSQL в понятное сообщение на русском,
        // чтобы в диалоге не появлялись «???» вместо осмысленного текста.
        private static string DescribeError(Exception ex, string databaseName)
        {
            if (ex is PostgresException pex)
            {
                switch (pex.SqlState)
                {
                    case "3D000":
                        return string.IsNullOrWhiteSpace(databaseName)
                            ? "База данных на сервере не задана или не существует. " +
                              "Нажмите «Подключиться» — она будет создана автоматически."
                            : $"Базы данных «{databaseName}» нет на сервере. " +
                              "Нажмите «Подключиться» — она будет создана автоматически.";
                    case "28P01":
                        return "PostgreSQL отклонил пароль. Проверьте имя пользователя и пароль.";
                    case "28000":
                        return "Не удалось войти в PostgreSQL. Проверьте имя пользователя и пароль.";
                    case "53300":
                        return "Превышено максимально допустимое число подключений к PostgreSQL.";
                    default:
                        return "Ошибка PostgreSQL: " + SafeText(pex.MessageText) +
                               " (код " + pex.SqlState + ")";
                }
            }
            if (ex is NpgsqlException || ex is System.Net.Sockets.SocketException || ex is TimeoutException)
                return "Сервер PostgreSQL недоступен. Проверьте хост, порт и запущен ли сервер.";
            return "Произошла ошибка: " + SafeText(ex.Message);
        }

        // Если сервер вернул текст в неподходящей кодировке (кракозябры/«???»),
        // показываем вместо него нейтральное сообщение.
        private static string SafeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "неизвестная ошибка";
            bool looksBroken = true;
            foreach (var ch in text)
            {
                if (ch != '?' && !char.IsControl(ch))
                {
                    looksBroken = false;
                    break;
                }
            }
            return looksBroken ? "сообщение сервера не удалось отобразить" : text;
        }
    }
}
