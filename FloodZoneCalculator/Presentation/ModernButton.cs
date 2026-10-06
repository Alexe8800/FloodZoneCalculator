#nullable enable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FloodZoneCalculator.Presentation;

/// <summary>
/// Современная кнопка: скруглённая, градиентная, с эффектом поднятия при наведении.
/// </summary>
public class ModernButton : Button
{
    private Color _baseColor;
    private Color _textColor;
    private bool _isHovered;
    private bool _isFocused;
    private bool _isPressed;

    public ModernButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        FlatStyle = FlatStyle.Flat;
        Cursor = Cursors.Hand;
        BackColor = Color.Transparent;
        ForeColor = Color.Transparent;
        Font = ModernTheme.FontBold;
        Padding = new Padding(14, 8, 14, 8);
        Height = 36;
        _baseColor = ModernTheme.Primary;
        _textColor = Color.FromArgb(255, 255, 255);
    }

    public Color AccentColor
    {
        get => _baseColor;
        set { _baseColor = value; Invalidate(); }
    }

    public Color TextColor
    {
        get => _textColor;
        set { _textColor = value; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; _isPressed = false; Invalidate(); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); _isPressed = true; Invalidate(); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); _isPressed = false; Invalidate(); }
    protected override void OnEnter(EventArgs e) { base.OnEnter(e); _isFocused = true; Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); _isFocused = false; Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Cursor = Enabled ? Cursors.Hand : Cursors.Arrow; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var r = ClientRectangle;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        var hover = _isHovered && Enabled;
        var scale = (hover && !_isPressed) ? 1.025f : 1.0f;
        var radius = 8f;
        var shadow = hover ? 7f : 0f;

        // Тень при поднятии
        if (shadow > 0)
        {
            var sp = ModernTheme.ShadowPath(r, shadow, radius);
            using var pen = new Pen(Color.FromArgb(0, 0, 0, 45), 1f);
            g.DrawPath(pen, sp);
        }

        // Центрируем трансформацию (масштабирование вокруг центра кнопки)
        g.TranslateTransform(r.Width / 2f, r.Height / 2f);
        g.ScaleTransform(scale, scale);
        g.TranslateTransform(-r.Width / 2f, -r.Height / 2f);

        // Заполнение градиентом
        var fill = _isPressed ? ModernTheme.ShadeColor(_baseColor, -14) : _baseColor;
        using var grad = ModernTheme.Gradient(r, fill, ModernTheme.ShadeColor(_baseColor, 10),
            _isPressed ? LinearGradientOrientation.BottomRightToTopLeft : LinearGradientOrientation.TopLeftToBottomRight);
        g.FillPath(grad, ModernTheme.RoundRect(r, radius));

        // Верхне-левое «стеклянное» свечение
        using var shine = new SolidBrush(Color.FromArgb(28, 255, 255, 255));
        g.FillPath(shine, ModernTheme.RoundRect(r, radius));

        // Тонкая нижне-правая грань для глубины
        using var edge = new Pen(Color.FromArgb(40, 0, 0, 0), 1f);
        g.DrawPath(edge, ModernTheme.RoundRect(r, radius));

        // Обводка фокуса
        if (_isFocused && Enabled)
        {
            using var glow = new Pen(Color.FromArgb(60, _baseColor), 2f);
            g.DrawPath(glow, ModernTheme.RoundRect(r, radius));
        }

        // Текст
        var tr = r;
        tr.Inflate(-8, -3);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoClip };
        using var tb = new SolidBrush(_textColor);
        g.DrawString(Text, Font, tb, tr, sf);
    }
}