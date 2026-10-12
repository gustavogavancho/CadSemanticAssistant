using CadSemanticAssistant.Domain.Semantics;

namespace CadSemanticAssistant.Domain.Queries;

public sealed class SemanticQuery
{
    public required QueryIntent Intent { get; init; }

    public required SemanticEntityType EntityType { get; init; }

    public IReadOnlyCollection<QueryFilter> Filters { get; init; } = [];
}
