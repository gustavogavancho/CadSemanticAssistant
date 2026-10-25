using CadSemanticAssistant.Domain.Queries;

namespace CadSemanticAssistant.Application.Queries.Validation;

public interface ISemanticQueryValidator
{
    public SemanticQueryValidationResult Validate(
        SemanticQuery query);
}
