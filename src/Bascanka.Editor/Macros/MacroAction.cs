using System.Text.Json.Serialization;
using System.Windows.Forms;

namespace Bascanka.Editor.Macros;

/// <summary>
/// Identifies the type of action captured during macro recording.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MacroActionType
{
    /// <summary>Insert text at the current caret position.</summary>
    TypeText,

    /// <summary>Delete the character after the caret (Delete key).</summary>
    Delete,

    /// <summary>Delete the character before the caret (Backspace key).</summary>
    Backspace,

    /// <summary>Move the caret to a new position.</summary>
    MoveCaret,

    /// <summary>Select a range of text.</summary>
    Select,

    /// <summary>Perform a find operation.</summary>
    Find,

    /// <summary>Perform a find-and-replace operation.</summary>
    Replace,

    /// <summary>Execute a named editor command.</summary>
    Command,
}

/// <summary>
/// Represents a single atomic action captured during macro recording.
/// Each instance captures enough state to replay the action on any
/// editor instance.
/// </summary>
public sealed class MacroAction
{
    /// <summary>The type of action this instance represents.</summary>
    public MacroActionType ActionType { get; init; }

    /// <summary>
    /// The text to insert for <see cref="MacroActionType.TypeText"/> actions,
    /// or the search/replace text for <see cref="MacroActionType.Find"/>
    /// and <see cref="MacroActionType.Replace"/> actions.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// The key pressed for movement-based actions such as
    /// <see cref="MacroActionType.MoveCaret"/> or <see cref="MacroActionType.Delete"/>.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Keys? Key { get; init; }

    /// <summary>
    /// A target offset for caret-movement actions.
    /// </summary>
    public long? Offset { get; init; }

    /// <summary>
    /// The name of a registered editor command for
    /// <see cref="MacroActionType.Command"/> actions.
    /// </summary>
    public string? CommandName { get; init; }

    /// <summary>
    /// Additional key/value parameters that further qualify the action.
    /// For example, search options for Find/Replace, or arguments for
    /// named commands.
    /// </summary>
    public Dictionary<string, string>? Parameters { get; init; }

    /// <summary>
    /// Returns a human-readable summary of this action for display
    /// in macro-editing UIs.
    /// </summary>
    public override string ToString() => ActionType switch
    {
        MacroActionType.TypeText => $"Type: \"{Truncate(Text, 30)}\"",
        MacroActionType.Delete => "Delete",
        MacroActionType.Backspace => "Backspace",
        MacroActionType.MoveCaret => Key.HasValue ? $"Move: {Key.Value}" : $"GoTo: {Offset}",
        MacroActionType.Select => $"Select at {Offset}",
        MacroActionType.Find => $"Find: \"{Truncate(Text, 30)}\"",
        MacroActionType.Replace => $"Replace: \"{Truncate(Text, 30)}\"",
        MacroActionType.Command => $"Command: {CommandName}",
        _ => ActionType.ToString(),
    };

    private static string Truncate(string? s, int maxLength)
    {
        if (s is null) return "";
        return s.Length <= maxLength ? s : s[..maxLength] + "...";
    }
}
