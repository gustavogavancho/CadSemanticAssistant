namespace CadSemanticAssistant.Application.Queries.Validation;

public sealed class SemanticQueryValidationResult
{
    public SemanticQueryValidationResult(
        IReadOnlyCollection<SemanticQueryValidationError> errors)
    {
        Errors = errors;
    }

    public IReadOnlyCollection<SemanticQueryValidationError> Errors
    {
        get;
    }

    public bool IsValid =>
        Errors.Count == 0;
}
