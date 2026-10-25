namespace CadSemanticAssistant.Application.Queries.Validation;

public sealed record SemanticQueryValidationError(
    SemanticQueryValidationErrorCode Code,
    string Message,
    int? FilterIndex = null);
