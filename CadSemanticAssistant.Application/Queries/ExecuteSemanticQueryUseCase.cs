using CadSemanticAssistant.Application.Queries.Validation;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Queries;

namespace CadSemanticAssistant.Application.Queries;

public sealed class ExecuteSemanticQueryUseCase
{
    private readonly ISemanticQueryValidator _validator;

    private readonly ISemanticQueryEngine _queryEngine;

    public ExecuteSemanticQueryUseCase(
        ISemanticQueryValidator validator,
        ISemanticQueryEngine queryEngine)
    {
        _validator =
            validator;

        _queryEngine =
            queryEngine;
    }

    public SemanticQueryResult Execute(
        SemanticQuery query,
        IReadOnlyCollection<CadEntity> entities)
    {
        SemanticQueryValidationResult validationResult =
            _validator.Validate(
                query);

        if (!validationResult.IsValid)
        {
            throw new SemanticQueryValidationException(
                validationResult.Errors);
        }

        return _queryEngine.Execute(
            query,
            entities);
    }
}
