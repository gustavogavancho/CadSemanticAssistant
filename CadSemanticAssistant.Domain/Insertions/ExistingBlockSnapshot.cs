namespace CadSemanticAssistant.Domain.Insertions;

/// <summary>
/// Read-only projection of an existing CAD reference. An old drawing may have
/// no INSTANCE_CODE; this is permitted for reading and does not create a code.
/// </summary>
public sealed class ExistingBlockSnapshot
{
    public string BlockName { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string?> Attributes { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}
