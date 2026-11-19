namespace CadSemanticAssistant.Domain.Catalog;

public sealed class CatalogBlockDefinition
{
    public string Id { get; init; } = string.Empty;
    public string BlockName { get; init; } = string.Empty;
    public string SemanticType { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Layer { get; init; } = string.Empty;
    public CatalogFootprint Footprint { get; init; } = new();
    public CatalogAttributeDefinition[] Attributes { get; init; } = [];
    public bool InsertionEnabled { get; init; }
    public string[] AllowedParentModels { get; init; } = [];
}
