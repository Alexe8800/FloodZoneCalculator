#nullable enable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation;

/// <summary>
/// Современное текстовое поле: скруглённое, тёмный фон, СВЕТЛЫЙ читаемый текст.
/// Текст рисует нативный EDIT-контроль (UserPaint ВЫКЛЮЧЕН), поэтому ForeColor
/// гарантированно применяется к вводимому тексту — на тёмном фоне текст белый.
/// Рамка, подсветка фокуса и подсказка дорисовываются в WndProc после нативной отрисовки.
/// </summary>
public class ModernTextBox : TextBox
{
    private bool _focused;

    /// <summary>Подсказка внутри поля, когда оно пустое и не в фокусе.</summary>
    public string Placeholder { get; set; } = "";

    public ModernTextBox()
    {
        BackColor = Color.FromArgb(30, 41, 59);
        ForeColor = Color.FromArgb(241, 245, 249);
        Font = ModernTheme.FontMain;
        BorderStyle = BorderStyle.None;
        Cursor = Cursors.IBeam;
        // Важно: НЕ включаем UserPaint — иначе нативный EDIT не выводит текст нужным цветом.
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }

    // Текст и фон рисует нативный EDIT через ForeColor/BackColor.
    // При блокированном/только-для-чтения поле тоже остаётся читаемым.
    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        ForeColor = Color.FromArgb(241, 245, 249);
        BackColor = Enabled ? Color.FromArgb(30, 41, 59) : Color.FromArgb(45, 55, 75);
        Invalidate();
    }

    protected override void OnEnter(EventArgs e) { _focused = true; Invalidate(); base.OnEnter(e); }
    protected override void OnLeave(EventArgs e) { _focused = false; Invalidate(); base.OnLeave(e); }
    protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyTextMargins();
        ApplyRoundedRegion();
    }

    private const int EM_SETMARGINS = 0x00D3;
    private const int EC_LEFTMARGIN = 0x0001;
    private const int EC_RIGHTMARGIN = 0x0002;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    // Небольшые отступы текста слева/справа, чтобы его не срезало скруглением углов.
    private void ApplyTextMargins()
    {
        if (!IsHandleCreated) return;
        var lParam = (IntPtr)((10 << 16) | 10);
        SendMessage(Handle, EM_SETMARGINS, (IntPtr)(EC_LEFTMARGIN | EC_RIGHTMARGIN), lParam);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        ApplyRoundedRegion();
    }

    private void ApplyRoundedRegion()
    {
        if (!IsHandleCreated || Width <= 0 || Height <= 0) return;
        using var path = ModernTheme.RoundRect(new Rectangle(0, 0, Width, Height), 7f);
        var region = new Region(path);
        var old = Region;
        Region = region;
        old?.Dispose();
    }

    private const int WM_PAINT = 0x000F;

    protected override void WndProc(ref Message m)
    {
        // Сначала нативная отрисовка текста (белый текст на тёмном фоне).
        base.WndProc(ref m);

        if (m.Msg != WM_PAINT) return;

        // Поверх нативного текста рисуем рамку/фокус и подсказку.
        using var g = Graphics.FromHwnd(Handle);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var r = new Rectangle(0, 0, Width, Height);
        using (var path = ModernTheme.RoundRect(r, 7f))
        using (var pen = new Pen(_focused
            ? Color.FromArgb(150, 6, 182, 212)
            : Color.FromArgb(51, 65, 85), 1.4f))
        {
            g.DrawPath(pen, path);
        }

        if (!string.IsNullOrEmpty(Placeholder) && string.IsNullOrEmpty(Text) && !_focused && Enabled)
        {
            var fmt = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Near };
            using var ph = new SolidBrush(Color.FromArgb(140, 148, 163, 184));
            var area = new RectangleF(8, 0, Width - 14, Height);
            g.DrawString(Placeholder, Font, ph, area, fmt);
        }
    }
}
