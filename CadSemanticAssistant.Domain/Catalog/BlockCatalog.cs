namespace CadSemanticAssistant.Domain.Catalog;

public sealed class BlockCatalog
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string Artifact { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public CatalogGeometry Geometry { get; init; } = new();
    public CatalogInstanceIdentity InstanceIdentity { get; init; } = new();
    public CatalogBlockDefinition[] CatalogItems { get; init; } = [];

    public CatalogBlockDefinition? FindByBlockName(string? blockName)
    {
        if (string.IsNullOrWhiteSpace(blockName))
        {
            return null;
        }

        return CatalogItems.FirstOrDefault(item =>
            string.Equals(item.BlockName, blockName, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class CatalogGeometry
{
    public string CoordinateSystem { get; init; } = string.Empty;
    public string Units { get; init; } = string.Empty;
    public int InsunitsExpected { get; init; }
    public string PositiveX { get; init; } = string.Empty;
    public string PositiveY { get; init; } = string.Empty;
    public string RotationUnit { get; init; } = string.Empty;
    public string FootprintReference { get; init; } = string.Empty;
    public double ToleranceMm { get; init; }
}

public sealed class CatalogInstanceIdentity
{
    public string Attribute { get; init; } = string.Empty;
    public string UniqueWithin { get; init; } = string.Empty;
    public bool CaseInsensitive { get; init; }
    public bool RequiredOnNewInsertions { get; init; }
    public string LegacyExistingWithoutCode { get; init; } = string.Empty;
}
