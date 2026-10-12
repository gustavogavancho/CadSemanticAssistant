namespace CadSemanticAssistant.Domain.Queries;

public sealed class QueryFilter
{
    public required string Property { get; init; }

    public required QueryOperator Operator { get; init; }

    public required object Value { get; init; }
}
