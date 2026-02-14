using System.Drawing;
using Bascanka.Core.Syntax;

namespace Bascanka.Editor.Themes;

/// <summary>
/// Defines the complete colour palette for the editor UI and syntax highlighting.
/// Implementations map every visual element and token type to a concrete colour,
/// allowing the rendering layer to remain theme-agnostic.
/// </summary>
public interface ITheme
{
    /// <summary>
    /// Human-readable name of this theme (e.g. "Dark", "Light", "Solarized").
    /// Used as the key for registration and selection in <see cref="ThemeManager"/>.
    /// </summary>
    string Name { get; }

    // ── Syntax highlighting ───────────────────────────────────────────

    /// <summary>
    /// Returns the foreground colour that should be used to render tokens
    /// of the given <paramref name="type"/>.
    /// </summary>
    Color GetTokenColor(TokenType type);

    // ── Editor surface ────────────────────────────────────────────────

    /// <summary>Background colour of the main text area.</summary>
    Color EditorBackground { get; }

    /// <summary>Default foreground colour for plain text.</summary>
    Color EditorForeground { get; }

    // ── Gutter (line numbers) ─────────────────────────────────────────

    /// <summary>Background colour of the line-number gutter.</summary>
    Color GutterBackground { get; }

    /// <summary>Foreground colour for line numbers.</summary>
    Color GutterForeground { get; }

    /// <summary>Foreground colour for the line number of the caret line.</summary>
    Color GutterCurrentLine { get; }

    // ── Current line / selection ──────────────────────────────────────

    /// <summary>Background highlight for the line containing the caret.</summary>
    Color LineHighlight { get; }

    /// <summary>Background colour of selected text.</summary>
    Color SelectionBackground { get; }

    /// <summary>Foreground colour of selected text.</summary>
    Color SelectionForeground { get; }

    // ── Caret ─────────────────────────────────────────────────────────

    /// <summary>Colour of the blinking text caret.</summary>
    Color CaretColor { get; }

    // ── Tab bar ───────────────────────────────────────────────────────

    /// <summary>Background colour of the tab bar strip.</summary>
    Color TabBarBackground { get; }

    /// <summary>Background colour of the active (selected) tab.</summary>
    Color TabActiveBackground { get; }

    /// <summary>Background colour of an inactive tab.</summary>
    Color TabInactiveBackground { get; }

    /// <summary>Foreground colour of the active tab label.</summary>
    Color TabActiveForeground { get; }

    /// <summary>Foreground colour of an inactive tab label.</summary>
    Color TabInactiveForeground { get; }

    /// <summary>Border colour between tabs.</summary>
    Color TabBorder { get; }

    // ── Status bar ────────────────────────────────────────────────────

    /// <summary>Background colour of the status bar.</summary>
    Color StatusBarBackground { get; }

    /// <summary>Foreground colour for status bar text.</summary>
    Color StatusBarForeground { get; }

    // ── Find / replace panel ──────────────────────────────────────────

    /// <summary>Background colour of the find/replace panel.</summary>
    Color FindPanelBackground { get; }

    /// <summary>Foreground colour for find/replace panel text.</summary>
    Color FindPanelForeground { get; }

    /// <summary>Background highlight colour for search match occurrences.</summary>
    Color MatchHighlight { get; }

    // ── Bracket matching ──────────────────────────────────────────────

    /// <summary>Background highlight for matched bracket pairs.</summary>
    Color BracketMatchBackground { get; }

    // ── Context menus ─────────────────────────────────────────────────

    /// <summary>Background colour of context menus.</summary>
    Color MenuBackground { get; }

    /// <summary>Foreground colour for context menu items.</summary>
    Color MenuForeground { get; }

    /// <summary>Background highlight for the hovered/selected menu item.</summary>
    Color MenuHighlight { get; }

    // ── Scroll bar ────────────────────────────────────────────────────

    /// <summary>Background colour of the scroll bar track.</summary>
    Color ScrollBarBackground { get; }

    /// <summary>Colour of the scroll bar thumb.</summary>
    Color ScrollBarThumb { get; }

    // ── Diff highlighting ────────────────────────────────────────────
    Color DiffAddedBackground { get; }
    Color DiffRemovedBackground { get; }
    Color DiffModifiedBackground { get; }
    Color DiffModifiedCharBackground { get; }
    Color DiffPaddingBackground { get; }
    Color DiffGutterMarker { get; }

    // ── Miscellaneous ─────────────────────────────────────────────────

    /// <summary>Colour of code-folding collapse/expand markers.</summary>
    Color FoldingMarker { get; }

    /// <summary>Colour of the unsaved-changes indicator (dirty dot).</summary>
    Color ModifiedIndicator { get; }
}
