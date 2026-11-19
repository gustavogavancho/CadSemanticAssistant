namespace CadSemanticAssistant.Domain.Catalog;

public sealed class CatalogAttributeDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool RequiredOnInsertion { get; init; }
    public string? FixedValue { get; init; }
    public string[]? AllowedValues { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public string? Pattern { get; init; }
    public string? Unit { get; init; }
    public string? Unique { get; init; }
    public string? ReferenceType { get; init; }
}
