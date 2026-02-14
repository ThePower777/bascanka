namespace Bascanka.Core.Diff;

public enum DiffLineType { Equal, Added, Removed, Modified, Padding }

public readonly struct CharDiffRange(int start, int length)
{
    public int Start { get; } = start;
    public int Length { get; } = length;
}

public sealed class DiffLine
{
    public DiffLineType Type { get; init; }
    public string Text { get; init; } = string.Empty;
    public int OriginalLineNumber { get; init; } // -1 for padding
    public List<CharDiffRange>? CharDiffs { get; init; }
}

public sealed class DiffSide
{
    public string Title { get; init; } = string.Empty;
    public string PaddedText { get; init; } = string.Empty;
    public DiffLine[] Lines { get; init; } = [];
}

public sealed class DiffResult
{
    public DiffSide Left { get; init; } = new();
    public DiffSide Right { get; init; } = new();
    public int[] DiffSectionStarts { get; init; } = [];
    public int DiffCount { get; init; }
}
