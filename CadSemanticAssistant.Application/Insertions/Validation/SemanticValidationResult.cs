namespace CadSemanticAssistant.Application.Insertions.Validation;

public sealed class SemanticValidationResult
{
    public SemanticValidationResult(IReadOnlyList<SemanticValidationIssue> issues)
    {
        Issues = issues ?? throw new ArgumentNullException(nameof(issues));
    }

    public IReadOnlyList<SemanticValidationIssue> Issues { get; }
    public bool IsValid => Issues.Count == 0;
}
