using System.Drawing;
using Bascanka.Core.Buffer;
using Bascanka.Core.Search;
using Bascanka.Core.Syntax;
using Bascanka.Editor.Themes;

namespace Bascanka.Editor.Controls;

/// <summary>
/// The main rendering surface for the text editor.  Inherits from
/// <see cref="Control"/> with double-buffering enabled and paints only
/// the visible lines using GDI (<see cref="TextRenderer"/>) for
/// pixel-sharp text rendering.
/// </summary>
public sealed class EditorSurface : Control
{
    private static readonly TextFormatFlags DrawFlags =
        TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
        TextFormatFlags.PreserveGraphicsClipping;

    /// <summary>Horizontal padding in pixels between the gutter and text content.</summary>
    private const int TextLeftPadding = 6;

    private PieceTable? _document;
    private Font _editorFont;
    private ITheme _theme;
    private int _tabSize = 4;
    private bool _wordWrap;
    private bool _showWhitespace;

    // Calculated font metrics.
    private int _charWidth;
    private int _lineHeight;

    // References to collaborating managers.
    private CaretManager? _caret;
    private SelectionManager? _selection;
    private ScrollManager? _scroll;
    private FoldingManager? _folding;
    private TokenCache? _tokenCache;
    private ILexer? _lexer;

    // Mouse state for click and drag selection.
    private bool _mouseDown;
    private int _clickCount;
    private DateTime _lastClickTime;
    private Point _lastClickPoint;

    // ────────────────────────────────────────────────────────────────────
    //  Constructor
    // ────────────────────────────────────────────────────────────────────

    public EditorSurface()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        _editorFont = new Font("Consolas", 11f, FontStyle.Regular, GraphicsUnit.Point);
        _theme = new DarkTheme();

        BackColor = _theme.EditorBackground;
        Cursor = Cursors.IBeam;

        RecalcFontMetrics();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Properties
    // ────────────────────────────────────────────────────────────────────

    /// <summary>The document buffer to render.</summary>
    public PieceTable? Document
    {
        get => _document;
        set
        {
            _document = value;
            Invalidate();
        }
    }

    /// <summary>The monospace font used for rendering text.</summary>
    public new Font Font
    {
        get => _editorFont;
        set
        {
            _editorFont = value ?? throw new ArgumentNullException(nameof(value));
            RecalcFontMetrics();
            Invalidate();
        }
    }

    /// <summary>Number of spaces per tab stop.</summary>
    public int TabSize
    {
        get => _tabSize;
        set
        {
            _tabSize = Math.Max(1, value);
            Invalidate();
        }
    }

    /// <summary>The active colour theme.</summary>
    public ITheme Theme
    {
        get => _theme;
        set
        {
            _theme = value ?? throw new ArgumentNullException(nameof(value));
            BackColor = _theme.EditorBackground;
            Invalidate();
        }
    }

    /// <summary>Whether word wrap is enabled.</summary>
    public bool WordWrap
    {
        get => _wordWrap;
        set
        {
            _wordWrap = value;
            Invalidate();
        }
    }

    /// <summary>Whether whitespace characters (spaces, tabs, line endings) are rendered.</summary>
    public bool ShowWhitespace
    {
        get => _showWhitespace;
        set
        {
            _showWhitespace = value;
            Invalidate();
        }
    }

    /// <summary>
    /// The maximum number of expanded columns that fit in the viewport.
    /// Used by word wrap to determine where to break lines.
    /// </summary>
    internal int WrapColumns => _charWidth > 0 ? Math.Max(20, ClientSize.Width / _charWidth) : 80;

    /// <summary>
    /// Returns the number of visual rows a document line occupies when word wrap is enabled.
    /// </summary>
    internal int GetWrapRowCount(long docLine)
    {
        if (!_wordWrap || _document is null || docLine >= _document.LineCount)
            return 1;
        string lineText = _document.GetLine(docLine);
        string expanded = ExpandTabs(lineText);
        int wrapCols = WrapColumns;
        return Math.Max(1, (expanded.Length + wrapCols - 1) / wrapCols);
    }

    /// <summary>Width of a single character cell in pixels.</summary>
    public int CharWidth => _charWidth;

    /// <summary>Height of a single line in pixels.</summary>
    public int LineHeight => _lineHeight;

    /// <summary>Number of fully visible lines in the viewport.</summary>
    public int VisibleLineCount => _lineHeight > 0 ? ClientSize.Height / _lineHeight : 1;

    /// <summary>Number of fully visible columns in the viewport.</summary>
    public int MaxVisibleColumns => _charWidth > 0 ? ClientSize.Width / _charWidth : 1;

    // ── Manager references ─────────────────────────────────────────────

    public CaretManager? Caret { get => _caret; set => _caret = value; }
    public SelectionManager? Selection { get => _selection; set => _selection = value; }
    public ScrollManager? Scroll { get => _scroll; set => _scroll = value; }
    public FoldingManager? Folding { get => _folding; set => _folding = value; }
    public TokenCache? Tokens { get => _tokenCache; set => _tokenCache = value; }
    public ILexer? Lexer { get => _lexer; set => _lexer = value; }

    /// <summary>The <see cref="InputHandler"/> that processes keyboard input.</summary>
    public InputHandler? InputHandler { get; set; }

    /// <summary>
    /// Compiled regex for highlighting search matches on visible lines.
    /// Set by the find panel; <see langword="null"/> when no search is active.
    /// </summary>
    public System.Text.RegularExpressions.Regex? SearchHighlightPattern { get; set; }

    // ────────────────────────────────────────────────────────────────────
    //  Font metrics
    // ────────────────────────────────────────────────────────────────────

    private void RecalcFontMetrics()
    {
        using var g = CreateGraphics();
        Size size = TextRenderer.MeasureText(g, "M", _editorFont, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        _charWidth = Math.Max(1, size.Width);
        _lineHeight = Math.Max(1, size.Height + 2);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Painting
    // ────────────────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.Clear(_theme.EditorBackground);

        if (_document is null || _lineHeight == 0) return;

        long firstVisible = _scroll?.FirstVisibleLine ?? 0;
        int hOffset = _wordWrap ? 0 : (_scroll?.HorizontalScrollOffset ?? 0);
        int visibleCount = VisibleLineCount + 1; // +1 for partial lines

        long totalDocLines = _document.LineCount;
        long caretLine = _caret?.Line ?? -1;

        // ── Pass 1: Determine which document lines are visible ──────────
        int entryCount = 0;
        long minDocLine = long.MaxValue, maxDocLine = long.MinValue;
        var docLines = new long[visibleCount];

        for (int i = 0; i < visibleCount; i++)
        {
            long docLine = _folding is not null
                ? _folding.VisibleLineToDocumentLine(firstVisible + i)
                : firstVisible + i;

            if (docLine >= totalDocLines) break;

            docLines[entryCount++] = docLine;
            if (docLine < minDocLine) minDocLine = docLine;
            if (docLine > maxDocLine) maxDocLine = docLine;
        }

        if (entryCount == 0)
        {
            RenderCaret(g, firstVisible, hOffset);
            return;
        }

        // ── Pass 2: Batch-fetch all lines in [min..max] ────────────────
        int rangeCount = (int)(maxDocLine - minDocLine + 1);
        var lineData = _document.GetLineRange(minDocLine, rangeCount);

        // ── Pre-create reusable GDI brushes ─────────────────────────────
        using var hlBrush = new SolidBrush(_theme.LineHighlight);
        using var selBrush = new SolidBrush(_theme.SelectionBackground);
        using var matchBrush = new SolidBrush(_theme.MatchHighlight);

        // ── Pass 3: Render each visible line ────────────────────────────
        int visualRow = 0;
        for (int i = 0; i < entryCount; i++)
        {
            long docLine = docLines[i];
            int dataIndex = (int)(docLine - minDocLine);
            if (dataIndex < 0 || dataIndex >= lineData.Length) continue;

            var (lineText, lineStartOffset) = lineData[dataIndex];

            if (_wordWrap)
            {
                string expanded = ExpandTabs(lineText);
                int wrapCols = WrapColumns;
                int rowsForLine = Math.Max(1, (expanded.Length + wrapCols - 1) / wrapCols);
                List<Token>? tokens = _tokenCache?.GetCachedTokens(docLine);

                for (int row = 0; row < rowsForLine; row++)
                {
                    int y = visualRow * _lineHeight;
                    if (y > ClientSize.Height) break;

                    int segStart = row * wrapCols;
                    int segLen = Math.Min(wrapCols, expanded.Length - segStart);

                    // Highlight current line (all wrap rows).
                    if (docLine == caretLine)
                        g.FillRectangle(hlBrush, 0, y, ClientSize.Width, _lineHeight);

                    // Selection for this wrap segment.
                    RenderWrapSelectionBackground(g, lineStartOffset, lineText,
                        segStart, segLen, y, selBrush);

                    // Text rendering.
                    if (segLen > 0)
                    {
                        string segment = expanded.Substring(segStart, segLen);
                        if (tokens is not null && tokens.Count > 0)
                            RenderTokenizedWrapSegment(g, lineText, expanded, tokens, segStart, segLen, y);
                        else
                            TextRenderer.DrawText(g, segment, _editorFont,
                                new Point(TextLeftPadding, y), _theme.EditorForeground, DrawFlags);
                    }

                    // Render whitespace glyphs if enabled.
                    if (_showWhitespace)
                    {
                        bool isLastSegment = (row == rowsForLine - 1);
                        RenderWhitespaceWrap(g, lineText, expanded, segStart, segLen, y,
                            docLine == totalDocLines - 1, isLastSegment);
                    }

                    visualRow++;
                }
            }
            else
            {
                int y = visualRow * _lineHeight;

                // Highlight current line.
                if (docLine == caretLine)
                    g.FillRectangle(hlBrush, 0, y, ClientSize.Width, _lineHeight);

                // Render search match highlights for this line.
                RenderMatchHighlights(g, lineText, y, hOffset, matchBrush);

                // Render selection background for this line.
                RenderSelectionBackground(g, lineText, lineStartOffset, y, hOffset, selBrush);

                // Render column selection background for this line.
                RenderColumnSelectionBackground(g, lineText, docLine, y, hOffset, selBrush);

                // Get tokens for syntax highlighting.
                List<Token>? tokens = _tokenCache?.GetCachedTokens(docLine);

                if (tokens is not null && tokens.Count > 0)
                    RenderTokenizedLine(g, lineText, tokens, y, hOffset);
                else
                    RenderPlainLine(g, lineText, y, hOffset);

                // Render whitespace glyphs if enabled.
                if (_showWhitespace)
                    RenderWhitespace(g, lineText, y, hOffset, docLine == totalDocLines - 1);

                // Render fold ellipsis indicator if next lines are collapsed.
                if (_folding is not null && _folding.IsFoldStart(docLine) && _folding.IsCollapsed(docLine))
                {
                    int textEndX = ExpandTabs(lineText).Length * _charWidth - hOffset * _charWidth + TextLeftPadding;
                    string indicator = " ... ";
                    using var bgBrush = new SolidBrush(Color.FromArgb(60, 128, 128, 128));
                    int indicatorWidth = indicator.Length * _charWidth;
                    g.FillRectangle(bgBrush, textEndX + 4, y, indicatorWidth, _lineHeight);
                    TextRenderer.DrawText(g, indicator, _editorFont,
                        new Point(textEndX + 4, y), _theme.GutterForeground, DrawFlags);
                }

                visualRow++;
            }
        }

        // Render caret.
        if (_wordWrap)
            RenderCaretWrapped(g, firstVisible, lineData, minDocLine, docLines, entryCount);
        else
            RenderCaret(g, firstVisible, hOffset);
    }

    private void RenderPlainLine(Graphics g, string lineText, int y, int hOffset)
    {
        string expanded = ExpandTabs(lineText);
        int startCol = hOffset;

        if (startCol >= expanded.Length) return;

        string visible = expanded.Substring(startCol,
            Math.Min(MaxVisibleColumns + 1, expanded.Length - startCol));

        TextRenderer.DrawText(g, visible, _editorFont,
            new Point(TextLeftPadding, y), _theme.EditorForeground, DrawFlags);
    }

    private void RenderTokenizedLine(Graphics g, string lineText, List<Token> tokens, int y, int hOffset)
    {
        // Expand tabs for accurate column positioning.
        string expanded = ExpandTabs(lineText);

        foreach (Token token in tokens)
        {
            int tokenStart = ExpandedColumn(lineText, token.Start);
            int tokenEnd = ExpandedColumn(lineText, token.End);

            // Clip to visible range.
            int drawStart = Math.Max(tokenStart - hOffset, 0);
            int drawEnd = Math.Min(tokenEnd - hOffset, MaxVisibleColumns + 1);

            if (drawEnd <= 0 || drawStart >= MaxVisibleColumns + 1) continue;

            int srcStart = Math.Max(tokenStart, hOffset);
            int srcEnd = Math.Min(tokenEnd, hOffset + MaxVisibleColumns + 1);

            if (srcStart >= expanded.Length || srcEnd <= srcStart) continue;

            string fragment = expanded.Substring(srcStart,
                Math.Min(srcEnd - srcStart, expanded.Length - srcStart));

            int x = (srcStart - hOffset) * _charWidth + TextLeftPadding;
            Color color = _theme.GetTokenColor(token.Type);

            TextRenderer.DrawText(g, fragment, _editorFont,
                new Point(x, y), color, DrawFlags);
        }
    }

    private void RenderSelectionBackground(Graphics g, string lineText, long lineStartOffset, int y, int hOffset, Brush selBrush)
    {
        if (_selection is null || !_selection.HasSelection) return;

        long lineStart = lineStartOffset;
        long lineEnd = lineStart + lineText.Length;

        long selStart = _selection.SelectionStart;
        long selEnd = _selection.SelectionEnd;

        if (selEnd <= lineStart || selStart >= lineEnd + 1) return;

        long drawSelStart = Math.Max(selStart, lineStart) - lineStart;
        long drawSelEnd = Math.Min(selEnd, lineEnd) - lineStart;

        int expandedStart = ExpandedColumn(lineText, (int)drawSelStart) - hOffset;
        int expandedEnd = ExpandedColumn(lineText, (int)drawSelEnd) - hOffset;

        int x1 = Math.Max(0, expandedStart * _charWidth) + TextLeftPadding;
        int x2 = Math.Max(x1, expandedEnd * _charWidth + TextLeftPadding);

        // If selection extends past line end (i.e. includes the newline), extend slightly.
        if (selEnd > lineEnd)
            x2 = Math.Max(x2, x2 + _charWidth);

        g.FillRectangle(selBrush, x1, y, x2 - x1, _lineHeight);
    }

    private void RenderColumnSelectionBackground(Graphics g, string lineText,
        long docLine, int y, int hOffset, Brush selBrush)
    {
        if (_selection is null || !_selection.HasColumnSelection) return;
        if (docLine < _selection.ColumnStartLine || docLine > _selection.ColumnEndLine) return;

        // Column selection stores visual (expanded) columns directly.
        int leftCol = (int)_selection.ColumnLeftCol;
        int rightCol = (int)_selection.ColumnRightCol;

        int x1 = Math.Max(0, leftCol - hOffset) * _charWidth + TextLeftPadding;
        int x2 = Math.Max(x1, (rightCol - hOffset) * _charWidth + TextLeftPadding);

        if (x2 > x1)
            g.FillRectangle(selBrush, x1, y, x2 - x1, _lineHeight);
    }

    private void RenderMatchHighlights(Graphics g, string lineText, int y, int hOffset, Brush matchBrush)
    {
        if (SearchHighlightPattern is null) return;

        foreach (System.Text.RegularExpressions.Match m in SearchHighlightPattern.Matches(lineText))
        {
            if (m.Length == 0) continue;

            int expandedStart = ExpandedColumn(lineText, m.Index) - hOffset;
            int expandedEnd = ExpandedColumn(lineText, m.Index + m.Length) - hOffset;

            int x1 = Math.Max(0, expandedStart * _charWidth) + TextLeftPadding;
            int x2 = Math.Max(x1, expandedEnd * _charWidth + TextLeftPadding);

            g.FillRectangle(matchBrush, x1, y, x2 - x1, _lineHeight);
        }
    }

    private void RenderCaret(Graphics g, long firstVisibleLine, int hOffset)
    {
        if (_caret is null || !_caret.IsVisible) return;

        long caretLine = _caret.Line;
        long caretCol = _caret.Column;

        long visibleLine;
        if (_folding is not null)
            visibleLine = _folding.DocumentLineToVisibleLine(caretLine);
        else
            visibleLine = caretLine;

        if (visibleLine < firstVisibleLine ||
            visibleLine >= firstVisibleLine + VisibleLineCount)
            return;

        int y = (int)(visibleLine - firstVisibleLine) * _lineHeight;

        // Expand tabs to get the correct pixel position.
        string lineText = _document is not null && caretLine < _document.LineCount
            ? _document.GetLine(caretLine)
            : string.Empty;

        int expandedCol = ExpandedColumn(lineText, (int)Math.Min(caretCol, lineText.Length));
        int x = (expandedCol - hOffset) * _charWidth + TextLeftPadding;

        if (x < 0 || x > ClientSize.Width) return;

        using var caretPen = new Pen(_theme.CaretColor, 2);
        g.DrawLine(caretPen, x, y, x, y + _lineHeight);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Word wrap helpers
    // ────────────────────────────────────────────────────────────────────

    private void RenderTokenizedWrapSegment(Graphics g, string lineText, string expanded,
        List<Token> tokens, int segStart, int segLen, int y)
    {
        int segEnd = segStart + segLen;

        foreach (Token token in tokens)
        {
            // Convert raw char indices to expanded column positions.
            int tokStart = ExpandedColumn(lineText, token.Start);
            int tokEnd = ExpandedColumn(lineText, token.End);

            // Clamp to this segment.
            int drawStart = Math.Max(tokStart, segStart);
            int drawEnd = Math.Min(tokEnd, segEnd);

            if (drawStart >= drawEnd) continue;

            string fragment = expanded.Substring(drawStart,
                Math.Min(drawEnd - drawStart, expanded.Length - drawStart));
            int x = (drawStart - segStart) * _charWidth + TextLeftPadding;
            Color color = _theme.GetTokenColor(token.Type);

            TextRenderer.DrawText(g, fragment, _editorFont,
                new Point(x, y), color, DrawFlags);
        }
    }

    private void RenderWrapSelectionBackground(Graphics g, long lineStartOffset,
        string lineText, int segStartExpanded, int segLen, int y, Brush selBrush)
    {
        if (_selection is null || !_selection.HasSelection) return;

        long lineStart = lineStartOffset;
        long lineEnd = lineStart + lineText.Length;

        long selStart = _selection.SelectionStart;
        long selEnd = _selection.SelectionEnd;

        if (selEnd <= lineStart || selStart >= lineEnd + 1) return;

        // Get selection range within line in expanded column space.
        long localSelStart = Math.Max(selStart - lineStart, 0);
        long localSelEnd = Math.Min(selEnd - lineStart, lineText.Length);

        int expSelStart = ExpandedColumn(lineText, (int)localSelStart);
        int expSelEnd = ExpandedColumn(lineText, (int)localSelEnd);

        // Clamp to this wrap segment.
        int segEnd = segStartExpanded + segLen;
        int drawStart = Math.Max(expSelStart, segStartExpanded) - segStartExpanded;
        int drawEnd = Math.Min(expSelEnd, segEnd) - segStartExpanded;

        // If selection extends past line end and this is the last segment.
        if (selEnd > lineEnd && segEnd >= ExpandedColumn(lineText, lineText.Length))
            drawEnd = Math.Max(drawEnd, drawEnd + 1);

        if (drawEnd <= drawStart) return;

        int x1 = drawStart * _charWidth + TextLeftPadding;
        int x2 = drawEnd * _charWidth + TextLeftPadding;
        g.FillRectangle(selBrush, x1, y, x2 - x1, _lineHeight);
    }

    private void RenderCaretWrapped(Graphics g, long firstVisibleLine,
        (string Text, long StartOffset)[] lineData, long minDocLine,
        long[] docLines, int entryCount)
    {
        if (_caret is null || !_caret.IsVisible || _document is null) return;

        long caretLine = _caret.Line;
        long caretCol = _caret.Column;
        int wrapCols = WrapColumns;

        // Find the visual row for the caret.
        int visualRow = 0;
        for (int i = 0; i < entryCount; i++)
        {
            long docLine = docLines[i];
            int dataIndex = (int)(docLine - minDocLine);
            if (dataIndex < 0 || dataIndex >= lineData.Length)
            {
                visualRow++;
                continue;
            }

            string lineText = lineData[dataIndex].Text;
            string expanded = ExpandTabs(lineText);
            int rowsForLine = Math.Max(1, (expanded.Length + wrapCols - 1) / wrapCols);

            if (docLine == caretLine)
            {
                int expandedCol = ExpandedColumn(lineText, (int)Math.Min(caretCol, lineText.Length));
                int wrapRow = wrapCols > 0 ? expandedCol / wrapCols : 0;
                int colInRow = expandedCol - wrapRow * wrapCols;

                int y = (visualRow + wrapRow) * _lineHeight;
                int x = colInRow * _charWidth + TextLeftPadding;

                if (y >= 0 && y <= ClientSize.Height)
                {
                    using var caretPen = new Pen(_theme.CaretColor, 2);
                    g.DrawLine(caretPen, x, y, x, y + _lineHeight);
                }
                return;
            }

            visualRow += rowsForLine;
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Whitespace rendering
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Renders visible whitespace glyphs (middle dot for spaces, arrow for
    /// tabs, pilcrow for line endings) over the already-drawn text.
    /// </summary>
    private void RenderWhitespace(Graphics g, string lineText, int y, int hOffset, bool isLastLine)
    {
        if (!_showWhitespace) return;

        var wsColor = Color.FromArgb(100,
            _theme.EditorForeground.R,
            _theme.EditorForeground.G,
            _theme.EditorForeground.B);

        int col = 0;
        for (int i = 0; i < lineText.Length; i++)
        {
            char c = lineText[i];
            if (c == '\t')
            {
                int tabWidth = _tabSize - (col % _tabSize);
                int x = (col - hOffset) * _charWidth + TextLeftPadding;
                if (x >= 0 && x < ClientSize.Width)
                {
                    // Draw a right arrow (→) at the tab start position.
                    TextRenderer.DrawText(g, "\u2192", _editorFont,
                        new Point(x, y), wsColor, DrawFlags);
                }
                col += tabWidth;
            }
            else if (c == ' ')
            {
                int x = (col - hOffset) * _charWidth + TextLeftPadding;
                if (x >= 0 && x < ClientSize.Width)
                {
                    // Draw a centered middle dot (·).
                    TextRenderer.DrawText(g, "\u00B7", _editorFont,
                        new Point(x, y), wsColor, DrawFlags);
                }
                col++;
            }
            else if (c == '\r' || c == '\n')
            {
                // Skip — we draw the line ending indicator after the loop.
                break;
            }
            else
            {
                col++;
            }
        }

        // Draw line ending indicator (¶) after the last character.
        if (!isLastLine)
        {
            int x = (col - hOffset) * _charWidth + TextLeftPadding;
            if (x >= 0 && x < ClientSize.Width)
            {
                TextRenderer.DrawText(g, "\u00B6", _editorFont,
                    new Point(x, y), wsColor, DrawFlags);
            }
        }
    }

    /// <summary>
    /// Renders whitespace glyphs for a word-wrap segment.
    /// </summary>
    private void RenderWhitespaceWrap(Graphics g, string lineText, string expanded,
        int segStart, int segLen, int y, bool isLastLine, bool isLastSegment)
    {
        if (!_showWhitespace) return;

        var wsColor = Color.FromArgb(100,
            _theme.EditorForeground.R,
            _theme.EditorForeground.G,
            _theme.EditorForeground.B);

        int segEnd = segStart + segLen;

        // Walk the raw lineText and map each char to its expanded column.
        int col = 0;
        for (int i = 0; i < lineText.Length; i++)
        {
            char c = lineText[i];
            if (c == '\r' || c == '\n') break;

            if (c == '\t')
            {
                int tabWidth = _tabSize - (col % _tabSize);
                if (col >= segStart && col < segEnd)
                {
                    int x = (col - segStart) * _charWidth + TextLeftPadding;
                    TextRenderer.DrawText(g, "\u2192", _editorFont,
                        new Point(x, y), wsColor, DrawFlags);
                }
                col += tabWidth;
            }
            else if (c == ' ')
            {
                if (col >= segStart && col < segEnd)
                {
                    int x = (col - segStart) * _charWidth + TextLeftPadding;
                    TextRenderer.DrawText(g, "\u00B7", _editorFont,
                        new Point(x, y), wsColor, DrawFlags);
                }
                col++;
            }
            else
            {
                col++;
            }

            if (col >= segEnd) break;
        }

        // Line ending indicator on the last wrap segment.
        if (isLastSegment && !isLastLine)
        {
            // Find expanded length of text content (without line endings).
            int textEndCol = col;
            if (textEndCol >= segStart && textEndCol < segEnd)
            {
                int x = (textEndCol - segStart) * _charWidth + TextLeftPadding;
                TextRenderer.DrawText(g, "\u00B6", _editorFont,
                    new Point(x, y), wsColor, DrawFlags);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Tab expansion helpers
    // ────────────────────────────────────────────────────────────────────

    private string ExpandTabs(string text)
    {
        if (!text.Contains('\t')) return text;

        var sb = new System.Text.StringBuilder(text.Length + 16);
        int col = 0;
        foreach (char c in text)
        {
            if (c == '\t')
            {
                int spaces = _tabSize - (col % _tabSize);
                sb.Append(' ', spaces);
                col += spaces;
            }
            else
            {
                sb.Append(c);
                col++;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Converts a character index in the raw line text to the expanded
    /// column position (accounting for tab stops).
    /// </summary>
    private int ExpandedColumn(string text, int charIndex)
    {
        int col = 0;
        int limit = Math.Min(charIndex, text.Length);
        for (int i = 0; i < limit; i++)
        {
            if (text[i] == '\t')
                col += _tabSize - (col % _tabSize);
            else
                col++;
        }
        return col;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Mouse events
    // ────────────────────────────────────────────────────────────────────

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || _document is null ||
            _caret is null || _selection is null) return;

        Focus();

        // Click count detection for double/triple click.
        TimeSpan elapsed = DateTime.Now - _lastClickTime;
        if (elapsed.TotalMilliseconds < SystemInformation.DoubleClickTime &&
            Math.Abs(e.X - _lastClickPoint.X) < SystemInformation.DoubleClickSize.Width &&
            Math.Abs(e.Y - _lastClickPoint.Y) < SystemInformation.DoubleClickSize.Height)
        {
            _clickCount++;
        }
        else
        {
            _clickCount = 1;
        }

        _lastClickTime = DateTime.Now;
        _lastClickPoint = e.Location;

        long offset = HitTestOffset(e.X, e.Y);

        if (_clickCount == 2)
        {
            // Double-click: select word.
            _selection.SelectWord(offset);
            _caret.MoveTo(_selection.SelectionEnd);
        }
        else if (_clickCount >= 3)
        {
            // Triple-click: select line.
            var (line, _) = HitTestLineColumn(e.X, e.Y);
            _selection.SelectLine(line);
            _caret.MoveTo(_selection.SelectionEnd);
            _clickCount = 3; // cap
        }
        else
        {
            // Single click.
            if ((ModifierKeys & Keys.Alt) != 0 && !_wordWrap)
            {
                // Alt+click starts column (box) selection using visual columns.
                var (line, expCol) = HitTestLineExpandedColumn(e.X, e.Y);
                _selection.StartColumnSelection(line, expCol);
                // Move caret to the character position (clamped to line length).
                var (_, charCol) = HitTestLineColumn(e.X, e.Y);
                _caret.MoveToLineColumn(line, charCol);
                _mouseDown = true;
                Invalidate();
                return;
            }
            else if ((ModifierKeys & Keys.Shift) != 0)
            {
                // Shift+click extends selection.
                if (!_selection.HasSelection)
                    _selection.StartSelection(_caret.Offset);
                _caret.MoveTo(offset);
                _selection.ExtendSelection(offset);
            }
            else
            {
                _selection.ClearSelection();
                _caret.MoveTo(offset);
                _selection.StartSelection(offset);
            }
        }

        _mouseDown = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_mouseDown || _document is null || _caret is null || _selection is null)
            return;

        if (_selection.IsColumnMode)
        {
            var (line, expCol) = HitTestLineExpandedColumn(e.X, e.Y);
            _selection.ExtendColumnSelection(line, expCol);
            // Move caret to the character position (clamped to line length).
            var (_, charCol) = HitTestLineColumn(e.X, e.Y);
            _caret.MoveToLineColumn(line, charCol);
            Invalidate();
            return;
        }

        long offset = HitTestOffset(e.X, e.Y);
        _caret.MoveTo(offset);
        _selection.ExtendSelection(offset);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _mouseDown = false;
    }

    /// <summary>Raised when Ctrl+MouseWheel requests a zoom change (+1 or -1).</summary>
    public event Action<int>? ZoomRequested;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if ((ModifierKeys & Keys.Control) != 0)
        {
            // Ctrl+Wheel = zoom.
            ZoomRequested?.Invoke(e.Delta > 0 ? 1 : -1);
            return;
        }

        _scroll?.HandleMouseWheel(e.Delta);
        Invalidate();
    }

    // ────────────────────────────────────────────────────────────────────
    //  Keyboard events
    // ────────────────────────────────────────────────────────────────────

    protected override bool IsInputKey(Keys keyData)
    {
        // Ensure arrow keys, Tab, etc. are sent to the control.
        switch (keyData & Keys.KeyCode)
        {
            case Keys.Up:
            case Keys.Down:
            case Keys.Left:
            case Keys.Right:
            case Keys.Tab:
            case Keys.Home:
            case Keys.End:
            case Keys.PageUp:
            case Keys.PageDown:
            case Keys.Delete:
            case Keys.Back:
            case Keys.Enter:
                return true;
        }
        return base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        bool ctrl = e.Control;
        bool shift = e.Shift;
        bool alt = e.Alt;

        if (InputHandler is not null && InputHandler.ProcessKeyDown(e.KeyCode, ctrl, shift, alt))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _caret?.EnsureVisible(_scroll!, VisibleLineCount, MaxVisibleColumns);
            Invalidate();
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (InputHandler is not null && e.KeyChar >= 32)
        {
            InputHandler.ProcessCharInput(e.KeyChar);
            e.Handled = true;
            _caret?.EnsureVisible(_scroll!, VisibleLineCount, MaxVisibleColumns);
            Invalidate();
        }

        base.OnKeyPress(e);
    }

    // ────────────────────────────────────────────────────────────────────
    //  Hit testing
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a pixel coordinate to a (line, column) pair.
    /// </summary>
    public (long Line, long Column) HitTestLineColumn(int x, int y)
    {
        if (_document is null) return (0, 0);

        long firstVisible = _scroll?.FirstVisibleLine ?? 0;
        int hOffset = _wordWrap ? 0 : (_scroll?.HorizontalScrollOffset ?? 0);

        if (_wordWrap)
            return HitTestLineColumnWrapped(x, y, firstVisible);

        int lineIndex = _lineHeight > 0 ? y / _lineHeight : 0;
        long visibleLine = firstVisible + lineIndex;

        long docLine;
        if (_folding is not null)
            docLine = _folding.VisibleLineToDocumentLine(visibleLine);
        else
            docLine = visibleLine;

        docLine = Math.Clamp(docLine, 0, _document.LineCount - 1);

        int expandedCol = _charWidth > 0 ? (Math.Max(0, x - TextLeftPadding) / _charWidth) + hOffset : 0;

        // Convert expanded column back to character index, accounting for tabs.
        string lineText = _document.GetLine(docLine);
        int col = CompressedColumn(lineText, expandedCol);

        return (docLine, col);
    }

    private (long Line, long Column) HitTestLineColumnWrapped(int x, int y, long firstVisible)
    {
        int targetRow = _lineHeight > 0 ? y / _lineHeight : 0;
        int wrapCols = WrapColumns;
        long totalDocLines = _document!.LineCount;

        int visualRow = 0;
        for (long docLine = firstVisible; docLine < totalDocLines; docLine++)
        {
            string lineText = _document.GetLine(docLine);
            string expanded = ExpandTabs(lineText);
            int rowsForLine = Math.Max(1, (expanded.Length + wrapCols - 1) / wrapCols);

            if (targetRow < visualRow + rowsForLine)
            {
                int wrapRow = targetRow - visualRow;
                int expandedCol = wrapRow * wrapCols + (_charWidth > 0 ? Math.Max(0, x - TextLeftPadding) / _charWidth : 0);
                expandedCol = Math.Min(expandedCol, expanded.Length);
                int col = CompressedColumn(lineText, expandedCol);
                return (docLine, col);
            }

            visualRow += rowsForLine;
        }

        // Past end of document.
        long lastLine = Math.Max(0, totalDocLines - 1);
        string lastText = _document.GetLine(lastLine);
        return (lastLine, lastText.Length);
    }

    /// <summary>
    /// Converts a pixel coordinate to a (line, expandedColumn) pair.
    /// Unlike <see cref="HitTestLineColumn"/> the column is NOT compressed
    /// to a character index — it is the raw visual column, which may exceed
    /// the line length. Used for column (box) selection.
    /// </summary>
    public (long Line, int ExpandedColumn) HitTestLineExpandedColumn(int x, int y)
    {
        if (_document is null) return (0, 0);

        long firstVisible = _scroll?.FirstVisibleLine ?? 0;
        int hOffset = _wordWrap ? 0 : (_scroll?.HorizontalScrollOffset ?? 0);

        int lineIndex = _lineHeight > 0 ? y / _lineHeight : 0;
        long visibleLine = firstVisible + lineIndex;

        long docLine;
        if (_folding is not null)
            docLine = _folding.VisibleLineToDocumentLine(visibleLine);
        else
            docLine = visibleLine;

        docLine = Math.Clamp(docLine, 0, _document.LineCount - 1);

        int expandedCol = _charWidth > 0 ? (Math.Max(0, x - TextLeftPadding) / _charWidth) + hOffset : 0;

        return (docLine, expandedCol);
    }

    /// <summary>
    /// Converts a pixel coordinate to a document character offset.
    /// </summary>
    public long HitTestOffset(int x, int y)
    {
        if (_document is null) return 0;

        var (line, col) = HitTestLineColumn(x, y);
        return _document.LineColumnToOffset(line, col);
    }

    /// <summary>
    /// Converts an expanded column position back to a raw character index
    /// in the line, accounting for tab stops.
    /// </summary>
    private int CompressedColumn(string text, int expandedCol)
    {
        int col = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (col >= expandedCol) return i;

            if (text[i] == '\t')
                col += _tabSize - (col % _tabSize);
            else
                col++;
        }

        return text.Length;
    }

    // ────────────────────────────────────────────────────────────────────
    //  Cleanup
    // ────────────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _editorFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
