using CadSemanticAssistant.Application.Selection;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Queries;

namespace CadSemanticAssistant.Application.Queries;

public interface ISemanticQuerySession
{
    public SemanticQueryResult Execute(SemanticQuery query);
    public CadSelectionResult Select(IReadOnlyCollection<CadEntity> entities);
    public  void ClearSelection();
}
