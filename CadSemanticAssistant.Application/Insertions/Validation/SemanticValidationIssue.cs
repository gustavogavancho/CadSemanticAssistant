namespace CadSemanticAssistant.Application.Insertions.Validation;

/// <param name="RuleCode">Experimental rule ID: SEM-001 through SEM-008.</param>
/// <param name="Message">Human-readable technical diagnostic.</param>
/// <param name="ProposedIndex">Zero-based proposed instance index, or null for whole-document/request errors.</param>
/// <param name="AttributeName">Attribute involved, if any.</param>
public sealed record SemanticValidationIssue(
    string RuleCode,
    string Message,
    int? ProposedIndex = null,
    string? AttributeName = null);
