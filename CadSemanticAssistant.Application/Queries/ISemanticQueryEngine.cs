using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Queries;

namespace CadSemanticAssistant.Application.Queries;

public interface ISemanticQueryEngine
{
    public SemanticQueryResult Execute(SemanticQuery query, IReadOnlyCollection<CadEntity> entities);
}
