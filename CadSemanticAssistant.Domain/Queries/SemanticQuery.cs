using CadSemanticAssistant.Domain.Semantics;

namespace CadSemanticAssistant.Domain.Queries;

public sealed class SemanticQuery
{
    public required QueryIntent Intent { get; init; }

    public required SemanticEntityType EntityType { get; init; }

    public IReadOnlyCollection<QueryFilter> Filters { get; init; }
        = [];

    public IReadOnlyCollection<string> ScopeEntityIds { get; init; }
        = [];

    public SpatialFilter? SpatialFilter { get; init; }

    public bool IsScoped =>
        ScopeEntityIds.Count > 0;

    public bool HasSpatialFilter =>
        SpatialFilter is not null;
}
