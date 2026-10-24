using CadSemanticAssistant.Application.Semantics;

namespace CadSemanticAssistant.Presentation.Wpf.ViewModels;

public sealed class QueryPropertyOption
{
    public required string Name { get; init; }
    public required SemanticDataType DataType { get; init; }
    public string? Unit { get; init; }
    public string DisplayName => string.IsNullOrWhiteSpace(Unit) ? Name : $"{Name} ({Unit})";
}
