namespace CadSemanticAssistant.Domain.Insertions;

/// <summary>
/// A request with explicit instances: quantity is the number of entries.
/// A later conversational interpreter may expand a requested count into these
/// individual instances before validation.
/// </summary>
public sealed class BlockInsertionBatch
{
    public IReadOnlyList<BlockInsertionDraft> Proposed { get; init; } = Array.Empty<BlockInsertionDraft>();
    public IReadOnlyList<ExistingBlockSnapshot> Existing { get; init; } = Array.Empty<ExistingBlockSnapshot>();
}
