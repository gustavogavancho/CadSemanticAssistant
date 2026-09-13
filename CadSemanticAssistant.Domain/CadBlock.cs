namespace CadSemanticAssistant.Domain;

public sealed class CadBlock
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string RawName { get; init; }
    public required string Layer { get; init; }
    public required CadPoint Position { get; init; }
    public required double RotationRadians { get; init; }
    public bool IsDynamic { get; init; }
    public bool IsExternalReference { get; init; }
    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
}
