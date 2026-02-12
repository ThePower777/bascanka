using System.Globalization;
using System.Text;
using System.Web;

namespace Bascanka.App;

/// <summary>
/// Static helper methods for text transformations used by the Text menu.
/// Each method takes a string and returns the transformed result.
/// </summary>
internal static class TextTransformations
{
    // ── Case conversions ────────────────────────────────────────────

    public static string ToUpperCase(string text) => text.ToUpperInvariant();

    public static string ToLowerCase(string text) => text.ToLowerInvariant();

    public static string ToTitleCase(string text)
        => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower(CultureInfo.CurrentCulture));

    public static string SwapCase(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            sb.Append(char.IsUpper(c) ? char.ToLowerInvariant(c)
                     : char.IsLower(c) ? char.ToUpperInvariant(c)
                     : c);
        }
        return sb.ToString();
    }

    // ── Encoding ────────────────────────────────────────────────────

    public static string Base64Encode(string text)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    public static string Base64Decode(string text)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(text.Trim());
            return Encoding.UTF8.GetString(bytes);
        }
        catch (FormatException)
        {
            return text; // Return original if invalid Base64.
        }
    }

    public static string UrlEncode(string text) => Uri.EscapeDataString(text);

    public static string UrlDecode(string text) => Uri.UnescapeDataString(text);

    public static string HtmlEncode(string text) => HttpUtility.HtmlEncode(text);

    public static string HtmlDecode(string text) => HttpUtility.HtmlDecode(text);

    // ── Line operations ─────────────────────────────────────────────

    public static string SortLinesAscending(string text)
    {
        var lines = SplitLines(text, out string eol);
        Array.Sort(lines, StringComparer.Ordinal);
        return string.Join(eol, lines);
    }

    public static string SortLinesDescending(string text)
    {
        var lines = SplitLines(text, out string eol);
        Array.Sort(lines, StringComparer.Ordinal);
        Array.Reverse(lines);
        return string.Join(eol, lines);
    }

    public static string RemoveDuplicateLines(string text)
    {
        var lines = SplitLines(text, out string eol);
        var seen = new HashSet<string>();
        var unique = new List<string>(lines.Length);
        foreach (string line in lines)
        {
            if (seen.Add(line))
                unique.Add(line);
        }
        return string.Join(eol, unique);
    }

    public static string ReverseLines(string text)
    {
        var lines = SplitLines(text, out string eol);
        Array.Reverse(lines);
        return string.Join(eol, lines);
    }

    // ── Whitespace ──────────────────────────────────────────────────

    public static string TrimTrailingWhitespace(string text)
    {
        var lines = SplitLines(text, out string eol);
        for (int i = 0; i < lines.Length; i++)
            lines[i] = lines[i].TrimEnd();
        return string.Join(eol, lines);
    }

    public static string TrimLeadingWhitespace(string text)
    {
        var lines = SplitLines(text, out string eol);
        for (int i = 0; i < lines.Length; i++)
            lines[i] = lines[i].TrimStart();
        return string.Join(eol, lines);
    }

    public static string CompactWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool lastWasSpace = false;
        foreach (char c in text)
        {
            if (c == ' ' || c == '\t')
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }
        return sb.ToString();
    }

    // ── Other ───────────────────────────────────────────────────────

    public static string ReverseText(string text)
    {
        // Reverse by grapheme clusters to handle surrogate pairs correctly.
        var elements = StringInfo.GetTextElementEnumerator(text);
        var list = new List<string>();
        while (elements.MoveNext())
            list.Add(elements.GetTextElement());
        list.Reverse();
        return string.Concat(list);
    }

    public static string TabsToSpaces(string text)
        => text.Replace("\t", "    ");

    public static string SpacesToTabs(string text)
        => text.Replace("    ", "\t");

    // ── Helpers ──────────────────────────────────────────────────────

    private static string[] SplitLines(string text, out string eol)
    {
        if (text.Contains("\r\n"))
            eol = "\r\n";
        else if (text.Contains('\n'))
            eol = "\n";
        else if (text.Contains('\r'))
            eol = "\r";
        else
            eol = "\n";

        return text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
    }
}
