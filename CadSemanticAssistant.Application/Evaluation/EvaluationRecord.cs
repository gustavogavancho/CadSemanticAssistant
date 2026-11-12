using CadSemanticAssistant.Domain.Queries;
using CadSemanticAssistant.Domain.Semantics;

namespace CadSemanticAssistant.Application.Evaluation;

/// <summary>
/// An anonymous trial-level measurement. Do not add natural-language text,
/// drawing names or paths, AutoCAD entity IDs, property values or credentials.
/// </summary>
public sealed record EvaluationRecord
{
    public Guid SessionId { get; init; }

    public required Guid TrialId { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    public required EvaluationStage Stage { get; init; }

    /// <summary>Technical success, not semantic accuracy.</summary>
    public required bool Success { get; init; }

    public long ElapsedMilliseconds { get; init; }

    public SemanticEntityType? EntityType { get; init; }

    public int? FilterCount { get; init; }

    public int? ResultCount { get; init; }

    public QueryScopeSource? ScopeSource { get; init; }

    public SpatialRelation? SpatialRelation { get; init; }

    public EvaluationFailureKind FailureKind { get; init; } = EvaluationFailureKind.None;

    /// <summary>Optional human assessment of whether the output matched expectations.</summary>
    public bool? UserConfirmedCorrect { get; init; }
}
