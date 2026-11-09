using CadSemanticAssistant.Application.Interaction;
using CadSemanticAssistant.Domain.Queries;

namespace CadSemanticAssistant.Application.NaturalLanguage;

public interface INaturalLanguageQueryInterpreter
{
    public Task<SemanticQuery> InterpretAsync(
        string text,
        InteractionContext context,
        CancellationToken cancellationToken = default);
}
