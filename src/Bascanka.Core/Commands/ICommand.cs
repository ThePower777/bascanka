namespace Bascanka.Core.Commands;

/// <summary>
/// Represents an undoable editing operation on a text document.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// A human-readable description of the operation (e.g. "Insert text", "Delete text").
    /// Used for UI display in undo/redo menus.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Performs the editing operation.
    /// </summary>
    void Execute();

    /// <summary>
    /// Reverses the editing operation, restoring the document to its prior state.
    /// </summary>
    void Undo();

    /// <summary>
    /// Determines whether <paramref name="other"/> can be merged into this command.
    /// This enables auto-grouping of consecutive character inserts or deletes
    /// so that each keystroke does not become a separate undo step.
    /// </summary>
    /// <param name="other">The candidate command to merge.</param>
    /// <returns><see langword="true"/> if the commands can be combined into one.</returns>
    bool CanMergeWith(ICommand other);

    /// <summary>
    /// Absorbs <paramref name="other"/> into this command.
    /// Callers must verify <see cref="CanMergeWith"/> returns <see langword="true"/>
    /// before calling this method.
    /// </summary>
    /// <param name="other">The command to absorb.</param>
    void MergeWith(ICommand other);
}
