namespace CadSemanticAssistant.Application.Queries.Validation;

public sealed class SemanticQueryValidationException
    : Exception
{
    public SemanticQueryValidationException(
        IReadOnlyCollection<SemanticQueryValidationError> errors)
        : base(CreateMessage(errors))
    {
        Errors = errors;
    }

    public IReadOnlyCollection<SemanticQueryValidationError> Errors
    {
        get;
    }

    private static string CreateMessage(
        IReadOnlyCollection<SemanticQueryValidationError> errors)
    {
        if (errors.Count == 0)
        {
            return "Semantic query validation failed.";
        }

        return string.Join(
            Environment.NewLine,

            errors.Select(
                error =>
                    error.Message));
    }
}
