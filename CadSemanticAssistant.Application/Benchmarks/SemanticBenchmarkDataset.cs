using CadSemanticAssistant.Domain.Insertions;

namespace CadSemanticAssistant.Application.Benchmarks;

/// <summary>Versioned deterministic acceptance dataset for SEM-001..SEM-008.</summary>
public sealed class SemanticBenchmarkDataset
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string DatasetId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Dictionary<string, ExistingBlockSnapshot[]> SceneSnapshots { get; init; } =
        new(StringComparer.Ordinal);
    public SemanticBenchmarkCase[] Cases { get; init; } = [];
}

public sealed class SemanticBenchmarkCase
{
    public string CaseId { get; init; } = string.Empty;
    public string? SourceScenarioId { get; init; }
    public string SourceCategory { get; init; } = string.Empty;
    public string UserUtterance { get; init; } = string.Empty;
    public string SceneId { get; init; } = string.Empty;
    public string ExpectedDecision { get; init; } = string.Empty;
    public string[] ExpectedRuleCodes { get; init; } = [];
    public BlockInsertionDraft[] Proposed { get; init; } = [];
}
