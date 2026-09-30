using CadSemanticAssistant.Domain.Semantics;

namespace CadSemanticAssistant.Application.Semantics.Configuration;

public sealed class EntityMappingRule
{
    public required SemanticEntityType EntityType { get; init; }

    public string[] BlockNamePatterns { get; init; } = [];

    public PropertyMappingRule[] Properties { get; init; } = [];
}
