using CadSemanticAssistant.Domain.Models;

namespace CadSemanticAssistant.Application.Interaction;

public sealed class InteractionContext
{
    public IReadOnlyCollection<CadEntity> SelectedEntities { get; init; }
        = [];

    public bool HasSelection =>
        SelectedEntities.Count > 0;
}
