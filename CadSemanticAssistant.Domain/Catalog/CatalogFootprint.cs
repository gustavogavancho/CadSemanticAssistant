namespace CadSemanticAssistant.Domain.Catalog;

public sealed class CatalogFootprint
{
    // "polygon" -> verticesLocalMm; "circle" -> centerLocalMm + radiusMm.
    public string Kind { get; init; } = string.Empty;
    public double[][]? VerticesLocalMm { get; init; }
    public double[]? CenterLocalMm { get; init; }
    public double? RadiusMm { get; init; }
    public string Anchor { get; init; } = string.Empty;
}
