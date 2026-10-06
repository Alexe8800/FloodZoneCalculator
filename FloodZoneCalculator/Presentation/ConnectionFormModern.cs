using FloodZoneCalculator.Infrastructure;
using FloodZoneDb.Client;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation;

public sealed class ConnectionFormModern : Form
{
    private readonly TextBox _connectionText;
    private readonly Label _status;
    private Button _connectButton = null!;

    public ConnectionFormModern()
    {
        ModernTheme.ApplyToForm(this);
        Text = "FloodZoneCalculator";
        Width = 860;
        Height = 480;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = false;

        // Фон с градиентом — «глубина океана»
        var bgPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ModernTheme.Background,
        };
        bgPanel.Paint += (s, e) =>
        {
            using var grad = new LinearGradientBrush(
                new Rectangle(0, 0, bgPanel.Width, bgPanel.Height),
                Color.FromArgb(10, 25, 47),
                Color.FromArgb(15, 23, 42), LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(grad, bgPanel.ClientRectangle);
        };

        // Левая декоративная панель
        var leftDecor = new Panel
        {
            Dock = DockStyle.Left,
            Width = 320,
            BackColor = Color.Transparent,
        };
        leftDecor.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Светящийся круг
            using var brush = new SolidBrush(Color.FromArgb(30, 6, 182, 212));
            g.FillEllipse(brush, -80, 60, 400, 400);
            using var brush2 = new SolidBrush(Color.FromArgb(20, 245, 158, 11));
            g.FillEllipse(brush2, 120, 280, 280, 280);
        };

        // Заголовок справа
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = Color.Transparent,
            Padding = new Padding(28, 24, 28, 10),
        };

        var titleLabel = new Label
        {
            Text = "Подключение к базе",
            Font = ModernTheme.FontHeading,
            ForeColor = Color.White,
            AutoSize = true,
            BackColor = Color.Transparent,
        };
        var subtitle = new Label
        {
            Text = "Гидравлический расчёт гидроузла на PostgreSQL 14+",
            Font = ModernTheme.FontSubHeading,
            ForeColor = ModernTheme.TextSub,
            AutoSize = true,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 6, 0, 0),
        };

        var headerFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        headerFlow.Controls.Add(titleLabel);
        headerFlow.Controls.Add(subtitle);
        header.Controls.Add(headerFlow);

        // Форма подключения
        var card = new Panel
        {
            Dock = DockStyle.Top,
            Height = 220,
            Margin = new Padding(28, 10, 28, 0),
            BackColor = ModernTheme.Surface,
            Padding = new Padding(22),
        };
        card.Paint += (s, e) =>
        {
            using var brush = new SolidBrush(Color.FromArgb(40, 51, 65, 85));
            e.Graphics.FillRoundedRectangle(brush, new Rectangle(0, 0, card.Width - 1, card.Height - 1), 14);
        };

        var labelConn = ModernTheme.MakeLabel("Строка подключения PostgreSQL:", false, false);
        labelConn.ForeColor = ModernTheme.TextMain;
        labelConn.Font = ModernTheme.FontBold;

        _connectionText = ModernTheme.MakeTextBox();
        _connectionText.Text = System.Environment.GetEnvironmentVariable("FLOODZONE_CONN")
            ?? "Host=localhost;Port=5432;Database=floodzone;Username=postgres;Password=1234";
        _connectionText.Height = 36;

        var hint = ModernTheme.MakeLabel("Если базы ещё нет — она будет создана автоматически и применятся миграции.", false, true);
        hint.Font = ModernTheme.FontSmall;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };
        _connectButton = ModernTheme.MakeButton("Подключиться и открыть объекты", true, 40);
        _connectButton.Width = 260;
        _connectButton.Click += async (s, e) => await ConnectAsync();
        actions.Controls.Add(_connectButton);

        _status = new Label
        {
            Text = "Готово к подключению.",
            Font = ModernTheme.FontSmall,
            ForeColor = ModernTheme.TextSub,
            AutoSize = true,
            Dock = DockStyle.Bottom,
        };

        // Раскладка внутри карточки
        var inner = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(0, 8, 0, 8),
        };
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        inner.Controls.Add(labelConn, 0, 0);
        inner.Controls.Add(_connectionText, 0, 1);
        inner.Controls.Add(hint, 0, 2);
        inner.Controls.Add(actions, 0, 3);
        inner.Controls.Add(_status, 0, 4);

        card.Controls.Add(inner);

        // Основной контейнер справа поверх фона
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Правый блок — заголовок + карточка
        var rightBlock = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        rightBlock.Controls.Add(header);
        rightBlock.Controls.Add(card);

        // Растягиваем карточку вниз
        card.Top = 130;
        card.Height = 220;
        card.Dock = DockStyle.None;

        // Фиксируем позицию заголовка и карточки вручную через Anchor/Location просто
        // Упростим — сбрасываем Dock для карточки в card.Top
        mainLayout.Controls.Add(leftDecor, 0, 0);
        mainLayout.Controls.Add(rightBlock, 1, 0);
        mainLayout.SetRowSpan(leftDecor, 2);

        bgPanel.Controls.Add(mainLayout);
        Controls.Add(bgPanel);
    }

    private async Task ConnectAsync()
    {
        try
        {
            _connectButton.Enabled = false;
            _status.Text = "Подключение и применение миграции...";
            _status.ForeColor = ModernTheme.PrimaryLight;

            var database = new FloodZoneConnection(_connectionText.Text);
            await database.EnsureDatabaseAndSchemaAsync();
            if (!await database.CanConnectAsync())
                throw new InvalidOperationException("PostgreSQL не подтвердил подключение.");

            AppLogger.Info("Подключение к БД и миграция завершены.");
            _status.Text = "Подключение успешно.";
            _status.ForeColor = ModernTheme.TextSub;
            Close();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Ошибка подключения или миграции.", ex);
            _status.Text = "Ошибка: " + ex.Message;
            _status.ForeColor = ModernTheme.Accent;
            MessageBox.Show(this, ex.Message, "Подключение к БД",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _connectButton.Enabled = true;
        }
    }
}

internal static class RoundedRectangleExtensions
{
    public static void FillRoundedRectangle(this Graphics g, Brush brush, Rectangle rect, int radius)
    {
        using var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
