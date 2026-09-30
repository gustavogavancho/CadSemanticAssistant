namespace CadSemanticAssistant.Domain.Semantics;

public sealed class SemanticPropertyValue
{
    public required object Value { get; init; }
    public required string RawValue { get; init; }
    public string? Unit { get; init; }
}
