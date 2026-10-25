using CadSemanticAssistant.Application.Semantics;
using CadSemanticAssistant.Application.Semantics.Configuration;
using CadSemanticAssistant.Domain.Queries;

namespace CadSemanticAssistant.Application.Queries.Validation;

public sealed class SemanticQueryValidator
    : ISemanticQueryValidator
{
    private readonly SemanticMappingConfiguration _configuration;

    public SemanticQueryValidator(
        SemanticMappingConfiguration configuration)
    {
        _configuration =
            configuration
            ?? throw new ArgumentNullException(
                nameof(configuration));
    }

    public SemanticQueryValidationResult Validate(
        SemanticQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<SemanticQueryValidationError> errors =
            [];

        ValidateIntent(
            query,
            errors);

        EntityMappingRule? entityMapping =
            FindEntityMapping(
                query);

        if (entityMapping is null)
        {
            errors.Add(
                new SemanticQueryValidationError(
                    SemanticQueryValidationErrorCode.UnsupportedEntityType,

                    $"Entity type '{query.EntityType}' " +
                    "is not configured in the semantic model."));

            return new SemanticQueryValidationResult(
                errors);
        }

        ValidateFilters(
            query,
            entityMapping,
            errors);

        return new SemanticQueryValidationResult(
            errors);
    }

    private static void ValidateIntent(
        SemanticQuery query,
        ICollection<SemanticQueryValidationError> errors)
    {
        if (Enum.IsDefined(
                query.Intent))
        {
            return;
        }

        errors.Add(
            new SemanticQueryValidationError(
                SemanticQueryValidationErrorCode.UnsupportedIntent,

                $"Query intent '{query.Intent}' is not supported."));
    }

    private EntityMappingRule? FindEntityMapping(
        SemanticQuery query)
    {
        return _configuration
            .EntityMappings
            .FirstOrDefault(
                mapping =>
                    mapping.EntityType ==
                    query.EntityType);
    }

    private static void ValidateFilters(
        SemanticQuery query,
        EntityMappingRule entityMapping,
        ICollection<SemanticQueryValidationError> errors)
    {
        int filterIndex =
            0;

        foreach (
            QueryFilter filter
            in query.Filters)
        {
            ValidateFilter(
                filter,
                filterIndex,
                entityMapping,
                errors);

            filterIndex++;
        }
    }

    private static void ValidateFilter(
        QueryFilter filter,
        int filterIndex,
        EntityMappingRule entityMapping,
        ICollection<SemanticQueryValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(
                filter.Property))
        {
            errors.Add(
                new SemanticQueryValidationError(
                    SemanticQueryValidationErrorCode.InvalidProperty,

                    "Filter property cannot be empty.",

                    filterIndex));

            return;
        }

        PropertyMappingRule? propertyMapping =
            entityMapping.Properties
                .FirstOrDefault(
                    property =>
                        string.Equals(
                            property.TargetProperty,
                            filter.Property,
                            StringComparison.OrdinalIgnoreCase));

        if (propertyMapping is null)
        {
            errors.Add(
                new SemanticQueryValidationError(
                    SemanticQueryValidationErrorCode.InvalidProperty,

                    $"Property '{filter.Property}' " +
                    $"is not valid for entity '{entityMapping.EntityType}'.",

                    filterIndex));

            return;
        }

        if (!IsOperatorSupported(
                propertyMapping.DataType,
                filter.Operator))
        {
            errors.Add(
                new SemanticQueryValidationError(
                    SemanticQueryValidationErrorCode.InvalidOperator,

                    $"Operator '{filter.Operator}' is not valid " +
                    $"for property '{propertyMapping.TargetProperty}' " +
                    $"of type '{propertyMapping.DataType}'.",

                    filterIndex));
        }

        if (!IsValueCompatible(
                propertyMapping.DataType,
                filter.Value))
        {
            string valueType =
                filter.Value?.GetType().Name
                ?? "null";

            errors.Add(
                new SemanticQueryValidationError(
                    SemanticQueryValidationErrorCode.InvalidValue,

                    $"Value for property " +
                    $"'{propertyMapping.TargetProperty}' " +
                    $"must be compatible with " +
                    $"'{propertyMapping.DataType}', " +
                    $"but received '{valueType}'.",

                    filterIndex));
        }
    }

    private static bool IsOperatorSupported(
        SemanticDataType dataType,
        QueryOperator queryOperator)
    {
        return dataType switch
        {
            SemanticDataType.String =>
                queryOperator is
                    QueryOperator.Equals
                    or QueryOperator.NotEquals
                    or QueryOperator.Contains,

            SemanticDataType.Integer =>
                IsNumericOperator(
                    queryOperator),

            SemanticDataType.Double =>
                IsNumericOperator(
                    queryOperator),

            SemanticDataType.Boolean =>
                queryOperator is
                    QueryOperator.Equals
                    or QueryOperator.NotEquals,

            _ =>
                false
        };
    }

    private static bool IsNumericOperator(
        QueryOperator queryOperator)
    {
        return queryOperator is
            QueryOperator.Equals
            or QueryOperator.NotEquals
            or QueryOperator.GreaterThan
            or QueryOperator.GreaterThanOrEqual
            or QueryOperator.LessThan
            or QueryOperator.LessThanOrEqual;
    }

    private static bool IsValueCompatible(
        SemanticDataType dataType,
        object? value)
    {
        if (value is null)
        {
            return false;
        }

        return dataType switch
        {
            SemanticDataType.String =>
                value is string,

            SemanticDataType.Integer =>
                IsInteger(
                    value),

            SemanticDataType.Double =>
                IsNumber(
                    value),

            SemanticDataType.Boolean =>
                value is bool,

            _ =>
                false
        };
    }

    private static bool IsInteger(
        object value)
    {
        return value is
            sbyte
            or byte
            or short
            or ushort
            or int
            or uint
            or long
            or ulong;
    }

    private static bool IsNumber(
        object value)
    {
        return IsInteger(value)
            ||
            value is
                float
                or double
                or decimal;
    }
}
