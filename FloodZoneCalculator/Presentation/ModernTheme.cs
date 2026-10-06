#nullable enable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation;

/// <summary>
/// Тёмная палитра «глубокий океан» и набор инструментов рисования (DesignKit).
/// </summary>
public static class ModernTheme
{
    // ---------------- Палитра ----------------

    // Фон / поверхности
    public static readonly Color Background = Color.FromArgb(15, 23, 42);      // slate-950
    public static readonly Color Surface = Color.FromArgb(30, 41, 59);         // slate-800
    public static readonly Color SurfaceLight = Color.FromArgb(51, 65, 85);    // slate-700
    public static readonly Color Panel = Color.FromArgb(22, 30, 48);           // чуть светлее фона

    // Акцент — глубокий циан / океан
    public static readonly Color Primary = Color.FromArgb(6, 182, 212);        // cyan-500
    public static readonly Color PrimaryLight = Color.FromArgb(34, 211, 238);   // cyan-400
    public static readonly Color PrimaryDark = Color.FromArgb(8, 145, 178);     // cyan-600
    public static readonly Color Cyan700 = Color.FromArgb(3, 148, 160);        // cyan-700 (выделение в таблицах)

    // Вторичный — тёплый янтарь
    public static readonly Color Accent = Color.FromArgb(245, 158, 11);        // amber-500
    public static readonly Color AccentLight = Color.FromArgb(251, 191, 36);    // amber-400
    public static readonly Color AccentDark = Color.FromArgb(202, 138, 4);      // amber-600

    // Статусные цвета
    public static readonly Color Success = Color.FromArgb(16, 185, 129);       // green-500
    public static readonly Color Warning = Color.FromArgb(245, 158, 11);       // amber-500
    public static readonly Color Error = Color.FromArgb(239, 68, 68);          // red-500

    // Текст
    public static readonly Color TextMain = Color.FromArgb(241, 245, 249);     // slate-100
    public static readonly Color TextSub = Color.FromArgb(148, 163, 184);      // slate-400
    public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);    // slate-500

    // Границы
    public static readonly Color Border = Color.FromArgb(51, 65, 85);          // slate-700
    public static readonly Color BorderLight = Color.FromArgb(71, 85, 105);    // slate-600
    public static readonly Color BorderDim = Color.FromArgb(39, 46, 59);       // slate-800 (тёмнее)

    // Шрифты
    public static readonly Font FontMain = new Font("Segoe UI", 9.5F, FontStyle.Regular);
    public static readonly Font FontBold = new Font("Segoe UI", 9.5F, FontStyle.Bold);
    public static readonly Font FontHeading = new Font("Segoe UI", 16F, FontStyle.Bold);
    public static readonly Font FontSubHeading = new Font("Segoe UI", 12F, FontStyle.Bold);
    public static readonly Font FontSmall = new Font("Segoe UI", 8.5F, FontStyle.Regular);

    // ---------------- DesignKit: примитивы рисования ----------------

    /// <summary>Градиентная кисть на прямоугольнике.</summary>
    public static LinearGradientBrush Gradient(RectangleF rect, Color c1, Color c2, LinearGradientOrientation orient)
    {
        var mode = orient switch
        {
            LinearGradientOrientation.TopToBottom => LinearGradientMode.Vertical,
            LinearGradientOrientation.BottomToTop => LinearGradientMode.Vertical,
            LinearGradientOrientation.LeftToRight => LinearGradientMode.Horizontal,
            LinearGradientOrientation.RightToLeft => LinearGradientMode.Horizontal,
            LinearGradientOrientation.TopLeftToBottomRight => LinearGradientMode.BackwardDiagonal,
            LinearGradientOrientation.BottomRightToTopLeft => LinearGradientMode.ForwardDiagonal,
            _ => LinearGradientMode.Vertical,
        };
        return new LinearGradientBrush(rect, c1, c2, mode);
    }

    public static LinearGradientBrush Gradient(Rectangle rect, Color c1, Color c2, LinearGradientOrientation orient) =>
        Gradient(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), c1, c2, orient);

    /// <summary>Затемнить/осветлить цвет, сдвигая каналы RGB.</summary>
    public static Color ShadeColor(Color c, int amount) =>
        Color.FromArgb(
            Math.Min(Math.Max(c.R + amount, 0), 255),
            Math.Min(Math.Max(c.G + amount, 0), 255),
            Math.Min(Math.Max(c.B + amount, 0), 255));

    /// <summary>Скруглённый прямоугольник (путь).</summary>
    public static GraphicsPath RoundRect(RectangleF r, float radius) =>
        RoundRect((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height, radius);

    public static GraphicsPath RoundRect(Rectangle rect, float radius) =>
        RoundRect(rect.X, rect.Y, rect.Width, rect.Height, radius);

    public static GraphicsPath RoundRect(int x, int y, int w, int h, float radius)
    {
        var p = new GraphicsPath(FillMode.Winding);
        if (radius <= 0f)
        {
            p.AddRectangle(new RectangleF(x, y, w, h));
            return p;
        }
        var rad = Math.Min(radius, Math.Min(w / 2f, h / 2f));
        p.AddLine(x + rad, y, x + w - rad, y);
        p.AddArc(x + w - 2 * rad, y, 2 * rad, 2 * rad, 270, 90);
        p.AddLine(x + w, y + rad, x + w, y + h - rad);
        p.AddArc(x + w - 2 * rad, y + h - 2 * rad, 2 * rad, 2 * rad, 0, 90);
        p.AddLine(x + w - rad, y + h, x + rad, y + h);
        p.AddArc(x, y + h - 2 * rad, 2 * rad, 2 * rad, 90, 90);
        p.AddLine(x, y + h - rad, x, y + rad);
        p.AddArc(x, y, 2 * rad, 2 * rad, 180, 90);
        p.CloseFigure();
        return p;
    }

    /// <summary>Путь тени (смещённый вниз и чуть больше оригинала).</summary>
    public static GraphicsPath ShadowPath(Rectangle rect, float offset, float radius)
    {
        var os = (int)Math.Ceiling(offset);
        var r = new Rectangle(rect.Left - os / 2, rect.Top + os, rect.Width + os, rect.Height + os);
        return RoundRect(r, radius);
    }

    // ---------------- Тема формы ----------------

    public static void ApplyToForm(Form f)
    {
        f.BackColor = Background;
        f.ForeColor = TextMain;
        f.Font = FontMain;
        f.Padding = new Padding(0);

        var db = typeof(Control).GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        db?.SetValue(f, true);
    }

    public static ModernButton MakeButton(string text, bool primary = true, int height = 36)
    {
        return new ModernButton
        {
            Text = text,
            Height = height,
            AccentColor = primary ? Primary : SurfaceLight,
            TextColor = primary ? Color.White : TextSub,
            Cursor = Cursors.Hand,
            Font = FontBold,
        };
    }

    public static Label MakeLabel(string text, bool isHeading = false, bool muted = false)
    {
        var lbl = new Label
        {
            Text = text,
            Font = isHeading ? FontHeading : FontMain,
            ForeColor = muted ? TextMuted : (isHeading ? TextMain : TextSub),
            AutoSize = true,
            BackColor = Color.Transparent,
        };
        return lbl;
    }

    public static ModernTextBox MakeTextBox(bool multiline = false)
    {
        var tb = new ModernTextBox
        {
            Multiline = multiline,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None,
            Font = FontMain,
        };
        if (multiline) { tb.Height = 60; }
        return tb;
    }

    public static ModernGroupBox MakeGroup(string title)
    {
        return new ModernGroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(12, 34, 12, 12), Margin = new Padding(4, 6, 4, 4) };
    }

    public static void ApplyToGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.ForeColor = TextMain;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = TextMain;
        grid.DefaultCellStyle.SelectionBackColor = Cyan700;
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.DefaultCellStyle.Font = FontMain;
        grid.DefaultCellStyle.NullValue = "—";

        grid.ColumnHeadersDefaultCellStyle.BackColor = PrimaryDark;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = FontBold;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.EnableHeadersVisualStyles = false;

        grid.GridColor = Border;
        grid.BorderStyle = BorderStyle.None;
        grid.RowHeadersVisible = false;
        grid.AllowUserToResizeRows = false;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
    }

    public static void ApplyToTree(TreeView tree)
    {
        tree.BackColor = Panel;
        tree.ForeColor = TextMain;
        tree.Font = FontMain;
        tree.BorderStyle = BorderStyle.None;
    }
}

/// <summary>Ориентация линейного градиента.</summary>
public enum LinearGradientOrientation
{
    TopToBottom,
    BottomToTop,
    LeftToRight,
    RightToLeft,
    TopLeftToBottomRight,
    BottomRightToTopLeft,
}
