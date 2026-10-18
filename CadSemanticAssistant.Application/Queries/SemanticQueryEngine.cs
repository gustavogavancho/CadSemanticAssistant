using System.Globalization;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Queries;
using CadSemanticAssistant.Domain.Semantics;

namespace CadSemanticAssistant.Application.Queries;

public sealed class SemanticQueryEngine : ISemanticQueryEngine
{
    public SemanticQueryResult Execute(SemanticQuery query, IReadOnlyCollection<CadEntity> entities)
    {
        IEnumerable<CadEntity> queryable = entities;

        queryable = queryable.Where(entity => entity.Type == query.EntityType);

        foreach (QueryFilter filter in query.Filters)
        {
            queryable = queryable.Where(entity => MatchesFilter(entity, filter));
        }

        IReadOnlyCollection<CadEntity> results = queryable.ToArray();

        return new SemanticQueryResult
        {
            Query = query,
            Entities = results
        };
    }

    private static bool MatchesFilter(CadEntity entity, QueryFilter filter)
    {
        bool propertyFound = entity.Properties.TryGetValue(filter.Property, out SemanticPropertyValue? property);

        if (!propertyFound || property is null)
        {
            return false;
        }

        return Compare(property.Value, filter.Operator, filter.Value);
    }

    private static bool Compare(object propertyValue, QueryOperator queryOperator, object queryValue)
    {
        return queryOperator switch
        {
            QueryOperator.Equals => AreEqual(propertyValue, queryValue),

            QueryOperator.NotEquals => !AreEqual(propertyValue, queryValue),

            QueryOperator.GreaterThan => CompareValues(propertyValue, queryValue) > 0,

            QueryOperator.GreaterThanOrEqual => CompareValues(propertyValue, queryValue) >= 0,

            QueryOperator.LessThan => CompareValues(propertyValue, queryValue) < 0,

            QueryOperator.LessThanOrEqual => CompareValues(propertyValue, queryValue) <= 0,

            QueryOperator.Contains => Contains(propertyValue, queryValue),

            _ => false
        };
    }

    private static bool AreEqual(
        object left,
        object right)
    {
        if (TryConvertToDecimal(
                left,
                out decimal leftNumber)
            &&
            TryConvertToDecimal(
                right,
                out decimal rightNumber))
        {
            return leftNumber == rightNumber;
        }

        return string.Equals(
            Convert.ToString(
                left,
                CultureInfo.InvariantCulture),
            Convert.ToString(
                right,
                CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareValues(
        object left,
        object right)
    {
        if (TryConvertToDecimal(
                left,
                out decimal leftNumber)
            &&
            TryConvertToDecimal(
                right,
                out decimal rightNumber))
        {
            return leftNumber.CompareTo(
                rightNumber);
        }

        string leftText =
            Convert.ToString(
                left,
                CultureInfo.InvariantCulture)
            ?? string.Empty;

        string rightText =
            Convert.ToString(
                right,
                CultureInfo.InvariantCulture)
            ?? string.Empty;

        return string.Compare(
            leftText,
            rightText,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(
        object propertyValue,
        object queryValue)
    {
        string propertyText =
            Convert.ToString(
                propertyValue,
                CultureInfo.InvariantCulture)
            ?? string.Empty;

        string queryText =
            Convert.ToString(
                queryValue,
                CultureInfo.InvariantCulture)
            ?? string.Empty;

        return propertyText.Contains(
            queryText,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryConvertToDecimal(
        object value,
        out decimal result)
    {
        switch (value)
        {
            case byte byteValue:
                result = byteValue;
                return true;

            case short shortValue:
                result = shortValue;
                return true;

            case int intValue:
                result = intValue;
                return true;

            case long longValue:
                result = longValue;
                return true;

            case float floatValue:
                result = Convert.ToDecimal(
                    floatValue,
                    CultureInfo.InvariantCulture);

                return true;

            case double doubleValue:
                result = Convert.ToDecimal(
                    doubleValue,
                    CultureInfo.InvariantCulture);

                return true;

            case decimal decimalValue:
                result = decimalValue;
                return true;

            default:
                result = default;
                return false;
        }
    }
}
