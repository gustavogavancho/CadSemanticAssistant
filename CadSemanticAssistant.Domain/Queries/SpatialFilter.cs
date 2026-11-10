namespace CadSemanticAssistant.Domain.Queries;

public sealed class SpatialFilter
{
    public required SpatialRelation Relation { get; init; }

    public IReadOnlyCollection<string> ReferenceEntityIds { get; init; }
        = [];

    // Same units as CadEntity.Position; v0.9 test DWG uses millimetres.
    public double? MaxDistance { get; init; }
}
