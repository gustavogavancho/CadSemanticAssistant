namespace CadSemanticAssistant.Application.Selection;

public sealed class CadSelectionResult
{
    public required int RequestedCount { get; init; }

    public required int SelectedCount { get; init; }

    public IReadOnlyCollection<string> UnresolvedEntityIds { get; init; } = [];
}
