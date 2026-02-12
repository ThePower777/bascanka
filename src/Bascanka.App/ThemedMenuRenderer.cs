using Bascanka.Editor.Themes;

namespace Bascanka.App;

/// <summary>
/// Custom <see cref="ToolStripProfessionalRenderer"/> that draws menus
/// and context menus using colours from the active <see cref="ITheme"/>.
/// </summary>
internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
{
    private readonly ITheme _theme;

    public ThemedMenuRenderer(ITheme theme)
        : base(new ThemedColorTable(theme))
    {
        _theme = theme;
        RoundedEdges = false;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Selected || e.Item.Pressed
            ? _theme.MenuForeground
            : _theme.MenuForeground;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var rect = new Rectangle(Point.Empty, e.Item.Size);

        if (e.Item.Selected || e.Item.Pressed)
        {
            using var brush = new SolidBrush(_theme.MenuHighlight);
            e.Graphics.FillRectangle(brush, rect);
        }
        else
        {
            using var brush = new SolidBrush(_theme.MenuBackground);
            e.Graphics.FillRectangle(brush, rect);
        }
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(_theme.MenuBackground);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // Draw a subtle border around dropdown menus.
        if (e.ToolStrip is ToolStripDropDownMenu)
        {
            Color borderColor = Lighten(_theme.MenuBackground, 40);
            using var pen = new Pen(borderColor);
            var rect = e.AffectedBounds;
            e.Graphics.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
        }
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        Color sepColor = Lighten(_theme.MenuBackground, 30);
        int y = e.Item.Height / 2;
        using var pen = new Pen(sepColor);
        e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = _theme.MenuForeground;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // Fill image margin with menu background to avoid white strip.
        using var brush = new SolidBrush(_theme.MenuBackground);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        // Draw checkmark with theme colours.
        var rect = e.ImageRectangle;
        using var brush = new SolidBrush(_theme.MenuHighlight);
        e.Graphics.FillRectangle(brush, rect);
        base.OnRenderItemCheck(e);
    }

    private static Color Lighten(Color c, int amount) =>
        Color.FromArgb(c.A,
            Math.Min(255, c.R + amount),
            Math.Min(255, c.G + amount),
            Math.Min(255, c.B + amount));

    /// <summary>
    /// Custom colour table that overrides the professional colour scheme
    /// with theme-aware colours.
    /// </summary>
    private sealed class ThemedColorTable : ProfessionalColorTable
    {
        private readonly ITheme _theme;

        public ThemedColorTable(ITheme theme) => _theme = theme;

        public override Color MenuStripGradientBegin => _theme.MenuBackground;
        public override Color MenuStripGradientEnd => _theme.MenuBackground;
        public override Color MenuItemSelected => _theme.MenuHighlight;
        public override Color MenuItemSelectedGradientBegin => _theme.MenuHighlight;
        public override Color MenuItemSelectedGradientEnd => _theme.MenuHighlight;
        public override Color MenuItemPressedGradientBegin => _theme.MenuHighlight;
        public override Color MenuItemPressedGradientEnd => _theme.MenuHighlight;
        public override Color MenuBorder => Lighten(_theme.MenuBackground, 40);
        public override Color MenuItemBorder => _theme.MenuHighlight;
        public override Color ImageMarginGradientBegin => _theme.MenuBackground;
        public override Color ImageMarginGradientMiddle => _theme.MenuBackground;
        public override Color ImageMarginGradientEnd => _theme.MenuBackground;
        public override Color SeparatorDark => Lighten(_theme.MenuBackground, 30);
        public override Color SeparatorLight => Lighten(_theme.MenuBackground, 30);
        public override Color ToolStripDropDownBackground => _theme.MenuBackground;
        public override Color ToolStripContentPanelGradientBegin => _theme.MenuBackground;
        public override Color ToolStripContentPanelGradientEnd => _theme.MenuBackground;
        public override Color CheckBackground => _theme.MenuHighlight;
        public override Color CheckSelectedBackground => _theme.MenuHighlight;
        public override Color CheckPressedBackground => _theme.MenuHighlight;

        private static Color Lighten(Color c, int amount) =>
            Color.FromArgb(c.A,
                Math.Min(255, c.R + amount),
                Math.Min(255, c.G + amount),
                Math.Min(255, c.B + amount));
    }
}
