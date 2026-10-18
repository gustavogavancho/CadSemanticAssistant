using CadSemanticAssistant.Domain.Models;

namespace CadSemanticAssistant.Application.Selection;

public interface ICadEntitySelectionService
{
    public CadSelectionResult Select(IReadOnlyCollection<CadEntity> entities);

    public void ClearSelection();
}
