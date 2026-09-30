namespace CadSemanticAssistant.Application.Semantics.Configuration;

public sealed class PropertyMappingRule
{
    public required string SourceAttribute { get; init; }
    public required string TargetProperty { get; init; }
    public SemanticDataType DataType { get; init; } = SemanticDataType.String;
    public string? Unit { get; init; }
    public bool Required { get; init; }
}
