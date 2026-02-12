using System.Drawing;
using System.Drawing.Text;

namespace Bascanka.Editor.Rendering;

/// <summary>
/// Singleton manager for the editor's monospace font.
/// Provides the current <see cref="Font"/>, pre-calculated character metrics,
/// and a <see cref="FontChanged"/> event that controls subscribe to for layout
/// recalculation.
/// </summary>
public sealed class FontManager : IDisposable
{
    // ── Singleton ─────────────────────────────────────────────────────

    private static readonly Lazy<FontManager> _lazy = new(() => new FontManager());

    /// <summary>
    /// The process-wide <see cref="FontManager"/> instance.
    /// </summary>
    public static FontManager Instance => _lazy.Value;

    // ── Constants ─────────────────────────────────────────────────────

    private const string DefaultFamilyName = "Consolas";
    private const string FallbackFamilyName = "Courier New";
    private const float DefaultSize = 11f;
    private const float MinFontSize = 6f;
    private const float MaxFontSize = 72f;

    // ── State ─────────────────────────────────────────────────────────

    private Font _currentFont;
    private int _charWidth;
    private int _lineHeight;
    private bool _disposed;

    private FontManager()
    {
        _currentFont = CreateFont(DefaultFamilyName, DefaultSize);
        RecalculateMetrics();
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// The monospace <see cref="Font"/> currently used by all editor views.
    /// </summary>
    public Font CurrentFont
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _currentFont;
        }
    }

    /// <summary>
    /// The point size of the current font.
    /// </summary>
    public float FontSize => _currentFont.Size;

    /// <summary>
    /// Width of a single character cell in pixels.
    /// Pre-calculated and cached so that callers do not need to measure text repeatedly.
    /// </summary>
    public int CharWidth => _charWidth;

    /// <summary>
    /// Height of a single line (character cell height) in pixels.
    /// </summary>
    public int LineHeight => _lineHeight;

    /// <summary>
    /// Raised after the font or its size changes.
    /// Controls should subscribe to this event and recalculate their layouts.
    /// </summary>
    public event EventHandler? FontChanged;

    /// <summary>
    /// Changes the editor font to the specified family and size.
    /// If <paramref name="familyName"/> is not available on the system,
    /// <c>Courier New</c> is used as a fallback.
    /// </summary>
    /// <param name="familyName">
    /// The font family name (e.g. "Cascadia Code", "JetBrains Mono").
    /// </param>
    /// <param name="size">
    /// Point size.  Clamped to the range [<c>6</c>, <c>72</c>].
    /// </param>
    public void SetFont(string familyName, float size)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyName);

        size = Math.Clamp(size, MinFontSize, MaxFontSize);

        Font newFont = CreateFont(familyName, size);

        Font oldFont = _currentFont;
        _currentFont = newFont;
        RecalculateMetrics();
        oldFont.Dispose();

        FontChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── IDisposable ───────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _currentFont.Dispose();
    }

    // ── Private helpers ───────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="Font"/> for the given family, falling back to
    /// <c>Courier New</c> if the requested family is not installed.
    /// </summary>
    private static Font CreateFont(string familyName, float size)
    {
        if (IsFontInstalled(familyName))
            return new Font(familyName, size, FontStyle.Regular, GraphicsUnit.Point);

        if (!string.Equals(familyName, FallbackFamilyName, StringComparison.OrdinalIgnoreCase)
            && IsFontInstalled(FallbackFamilyName))
        {
            return new Font(FallbackFamilyName, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        // Last resort: use the system's generic monospace font.
        return new Font(FontFamily.GenericMonospace, size, FontStyle.Regular, GraphicsUnit.Point);
    }

    /// <summary>
    /// Returns <c>true</c> if a font with the given family name is installed
    /// on the current system.
    /// </summary>
    private static bool IsFontInstalled(string familyName)
    {
        using var installed = new InstalledFontCollection();
        foreach (var family in installed.Families)
        {
            if (string.Equals(family.Name, familyName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Recalculates <see cref="CharWidth"/> and <see cref="LineHeight"/>
    /// from the current font using the same measuring logic as
    /// <see cref="EditorTextRenderer"/>.
    /// </summary>
    private void RecalculateMetrics()
    {
        _charWidth = EditorTextRenderer.MeasureCharWidth(_currentFont);
        _lineHeight = EditorTextRenderer.MeasureLineHeight(_currentFont);
    }
}
