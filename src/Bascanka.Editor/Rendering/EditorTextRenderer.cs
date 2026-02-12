using System.Drawing;
using System.Windows.Forms;
using Bascanka.Core.Syntax;
using Bascanka.Editor.Themes;

namespace Bascanka.Editor.Rendering;

/// <summary>
/// Provides static helper methods for rendering syntax-highlighted text,
/// selection highlights, and the current-line indicator using GDI text drawing
/// (<see cref="TextRenderer.DrawText(IDeviceContext, string, Font, Point, Color)"/>).
/// </summary>
/// <remarks>
/// This class is intentionally stateless. All state (font, theme, positions)
/// is passed in through method parameters so that multiple editor controls
/// can share the same rendering logic without interference.
/// </remarks>
public static class EditorTextRenderer
{
    // TextFormatFlags used for all text rendering.  NoPadding avoids the
    // small internal padding WinForms adds around each DrawText call,
    // giving us pixel-accurate positioning for monospace fonts.
    private const TextFormatFlags DrawFlags =
        TextFormatFlags.NoPadding |
        TextFormatFlags.NoPrefix |
        TextFormatFlags.PreserveGraphicsClipping;

    /// <summary>
    /// Renders a single line of text with syntax colouring.
    /// </summary>
    /// <param name="g">The <see cref="Graphics"/> surface to draw on.</param>
    /// <param name="text">The full text content of the line.</param>
    /// <param name="tokens">
    /// The list of <see cref="Token"/> spans produced by the lexer for this line.
    /// If <c>null</c> or empty the entire line is drawn with the editor foreground colour.
    /// </param>
    /// <param name="x">Pixel X origin (left edge) of the text area.</param>
    /// <param name="y">Pixel Y origin (top edge) of the line.</param>
    /// <param name="font">The monospace <see cref="Font"/> to use.</param>
    /// <param name="theme">The active <see cref="ITheme"/>.</param>
    public static void DrawLine(
        Graphics g,
        string text,
        List<Token>? tokens,
        int x,
        int y,
        Font font,
        ITheme theme)
    {
        ArgumentNullException.ThrowIfNull(g);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(theme);

        if (string.IsNullOrEmpty(text))
            return;

        int charWidth = MeasureCharWidth(font);

        if (tokens is null || tokens.Count == 0)
        {
            // No syntax information -- draw the entire line in the default colour.
            var point = new Point(x, y);
            TextRenderer.DrawText(g, text, font, point, theme.EditorForeground, DrawFlags);
            return;
        }

        int lastEnd = 0;

        foreach (var token in tokens)
        {
            // If there is a gap before this token (plain text not covered by any token),
            // draw it with the default editor foreground.
            if (token.Start > lastEnd)
            {
                DrawSpan(g, text, lastEnd, token.Start - lastEnd, x, y,
                         charWidth, font, theme.EditorForeground);
            }

            // Clamp the token to the actual line length to guard against lexer bugs.
            int tokenStart = Math.Min(token.Start, text.Length);
            int tokenLength = Math.Min(token.Length, text.Length - tokenStart);

            if (tokenLength > 0)
            {
                Color colour = theme.GetTokenColor(token.Type);
                DrawSpan(g, text, tokenStart, tokenLength, x, y,
                         charWidth, font, colour);
            }

            lastEnd = token.End;
        }

        // Draw any trailing text after the last token.
        if (lastEnd < text.Length)
        {
            DrawSpan(g, text, lastEnd, text.Length - lastEnd, x, y,
                     charWidth, font, theme.EditorForeground);
        }
    }

    /// <summary>
    /// Draws a rectangular selection highlight for a range of columns on a single line.
    /// </summary>
    /// <param name="g">The <see cref="Graphics"/> surface.</param>
    /// <param name="startCol">Zero-based column where the selection begins.</param>
    /// <param name="endCol">Zero-based column where the selection ends (exclusive).</param>
    /// <param name="y">Pixel Y origin of the line.</param>
    /// <param name="charWidth">Width of a single character in pixels.</param>
    /// <param name="lineHeight">Height of a single line in pixels.</param>
    /// <param name="selColor">The selection background colour.</param>
    public static void DrawSelection(
        Graphics g,
        int startCol,
        int endCol,
        int y,
        int charWidth,
        int lineHeight,
        Color selColor)
    {
        ArgumentNullException.ThrowIfNull(g);

        if (endCol <= startCol)
            return;

        int pixelX = startCol * charWidth;
        int width = (endCol - startCol) * charWidth;

        using var brush = new SolidBrush(selColor);
        g.FillRectangle(brush, pixelX, y, width, lineHeight);
    }

    /// <summary>
    /// Draws a full-width background highlight for the line containing the caret.
    /// </summary>
    /// <param name="g">The <see cref="Graphics"/> surface.</param>
    /// <param name="y">Pixel Y origin of the line.</param>
    /// <param name="width">Full width of the editor surface in pixels.</param>
    /// <param name="lineHeight">Height of a single line in pixels.</param>
    /// <param name="color">The line-highlight colour.</param>
    public static void DrawCurrentLineHighlight(
        Graphics g,
        int y,
        int width,
        int lineHeight,
        Color color)
    {
        ArgumentNullException.ThrowIfNull(g);

        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, 0, y, width, lineHeight);
    }

    /// <summary>
    /// Measures the width of a single character for the given monospace font.
    /// </summary>
    /// <remarks>
    /// Uses the capital letter "M" as the measurement reference because it
    /// gives the most consistent width across monospace font families.
    /// The result is cached per font via <see cref="TextRenderer.MeasureText"/>.
    /// </remarks>
    public static int MeasureCharWidth(Font font)
    {
        ArgumentNullException.ThrowIfNull(font);

        // Measure a single "M" with NoPadding to get the true character cell width.
        Size size = TextRenderer.MeasureText("M", font, Size.Empty, DrawFlags);
        return size.Width;
    }

    /// <summary>
    /// Measures the line height for the given font.
    /// </summary>
    public static int MeasureLineHeight(Font font)
    {
        ArgumentNullException.ThrowIfNull(font);

        // Use the font's height (cell ascent + descent + line gap) rounded up.
        return (int)Math.Ceiling(font.GetHeight());
    }

    // ── Private helpers ───────────────────────────────────────────────

    /// <summary>
    /// Draws a substring of <paramref name="text"/> starting at character index
    /// <paramref name="start"/> for <paramref name="length"/> characters, positioned
    /// using the fixed <paramref name="charWidth"/>.
    /// </summary>
    private static void DrawSpan(
        Graphics g,
        string text,
        int start,
        int length,
        int xOrigin,
        int y,
        int charWidth,
        Font font,
        Color colour)
    {
        string span = text.Substring(start, length);
        int pixelX = xOrigin + start * charWidth;
        var point = new Point(pixelX, y);
        TextRenderer.DrawText(g, span, font, point, colour, DrawFlags);
    }
}
