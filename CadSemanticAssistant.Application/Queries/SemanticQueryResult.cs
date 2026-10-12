using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Queries;

namespace CadSemanticAssistant.Application.Queries;

public sealed class SemanticQueryResult
{
    public required SemanticQuery Query { get; init; }

    public required IReadOnlyCollection<CadEntity> Entities { get; init; }

    public int Count => Entities.Count;
}
