namespace CadSemanticAssistant.Domain.Insertions;

/// <summary>One explicit block insertion, before geometric placement is calculated.</summary>
public sealed class BlockInsertionDraft
{
    public string BlockName { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string?> Attributes { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}
