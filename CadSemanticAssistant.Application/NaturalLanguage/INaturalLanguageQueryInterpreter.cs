using CadSemanticAssistant.Domain.Queries;

namespace CadSemanticAssistant.Infrastructure.AI.OpenAI.NaturalLanguage;

public interface INaturalLanguageQueryInterpreter
{
    public Task<SemanticQuery> InterpretAsync(string text, CancellationToken cancellationToken = default);
}
