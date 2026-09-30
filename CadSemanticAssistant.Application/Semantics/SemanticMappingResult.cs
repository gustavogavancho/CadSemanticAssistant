using CadSemanticAssistant.Domain.Models;

namespace CadSemanticAssistant.Application.Semantics;

public sealed class SemanticMappingResult
{
    public required CadEntity Entity { get; init; }

    public IReadOnlyCollection<string> Warnings { get; init; } = [];
}
