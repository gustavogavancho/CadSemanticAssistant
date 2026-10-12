using System.Globalization;
using CadSemanticAssistant.Application.Semantics.Configuration;
using CadSemanticAssistant.Application.Semantics.Internal;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Semantics;

namespace CadSemanticAssistant.Application.Semantics;

public sealed class RuleBasedCadSemanticMapper : IBlockSemanticMapper
{
    private readonly SemanticMappingConfiguration _configuration;

    public RuleBasedCadSemanticMapper(SemanticMappingConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SemanticMappingResult Map(CadBlock block)
    {
        List<string> warnings = [];

        EntityMappingRule? mappingRule = FindMappingRule(block);

        if (mappingRule is null)
        {
            CadEntity unknownEntity = CreateUnknownEntity(block);

            warnings.Add($"No semantic mapping rule matched block '{block.Name}'.");

            return new SemanticMappingResult
            {
                Entity = unknownEntity,
                Warnings = warnings
            };
        }

        Dictionary<string, SemanticPropertyValue> properties = MapProperties(block, mappingRule, warnings);

        CadEntity entity = new()
        {
            Id = block.Id,

            Type = mappingRule.EntityType,

            Name = block.Name,

            RawName = block.RawName,

            Layer = block.Layer,

            Position = block.Position,

            RotationRadians = block.RotationRadians,

            IsDynamic = block.IsDynamic,

            IsExternalReference = block.IsExternalReference,

            Properties = properties
        };

        return new SemanticMappingResult
        {
            Entity = entity,
            Warnings = warnings
        };
    }

    private EntityMappingRule? FindMappingRule(CadBlock block)
    {
        foreach (EntityMappingRule mappingRule in _configuration.EntityMappings)
        {
            foreach (string pattern in mappingRule.BlockNamePatterns)
            {
                bool matchesEffectiveName = WildcardMatcher.IsMatch(block.Name, pattern);

                bool matchesRawName = WildcardMatcher.IsMatch(block.RawName, pattern);

                if (matchesEffectiveName || matchesRawName)
                {
                    return mappingRule;
                }
            }
        }

        return null;
    }

    private static Dictionary<string, SemanticPropertyValue> MapProperties(CadBlock block, EntityMappingRule mappingRule, ICollection<string> warnings)
    {
        Dictionary<string, SemanticPropertyValue> properties = new(StringComparer.OrdinalIgnoreCase);

        foreach (PropertyMappingRule propertyRule in mappingRule.Properties)
        {
            bool found = TryGetAttributeValue(
                block.Attributes,
                propertyRule.SourceAttribute,
                out string? rawValue);

            if (!found || string.IsNullOrWhiteSpace(rawValue))
            {
                if (propertyRule.Required)
                {
                    warnings.Add($"Required attribute '{propertyRule.SourceAttribute}' " + $"was not found on block '{block.Name}'.");
                }

                continue;
            }

            string normalizedValue = rawValue.Trim();

            bool converted = TryConvertValue(
                normalizedValue,
                propertyRule.DataType,
                out object? value);

            if (!converted || value is null)
            {
                warnings.Add($"Attribute '{propertyRule.SourceAttribute}' " + $"with value '{rawValue}' could not be converted " + $"to {propertyRule.DataType}.");

                continue;
            }

            SemanticPropertyValue semanticValue = new()
            {
                Value = value,
                RawValue = normalizedValue,
                Unit = propertyRule.Unit
            };

            properties[propertyRule.TargetProperty] = semanticValue;
        }

        return properties;
    }

    private static bool TryGetAttributeValue(
        IReadOnlyDictionary<string, string> attributes,
        string attributeName,
        out string? value)
    {
        string normalizedName = attributeName.Trim();

        foreach (KeyValuePair<string, string> attribute in attributes)
        {
            if (string.Equals(
                    attribute.Key.Trim(),
                    normalizedName,
                    StringComparison.OrdinalIgnoreCase))
            {
                value = attribute.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool TryConvertValue(string rawValue, SemanticDataType dataType, out object? value)
    {
        switch (dataType)
        {
            case SemanticDataType.String:
                value = rawValue;
                return true;

            case SemanticDataType.Integer:
                return TryParseInteger(
                    rawValue,
                    out value);

            case SemanticDataType.Double:
                return TryParseDouble(
                    rawValue,
                    out value);

            case SemanticDataType.Boolean:
                return TryParseBoolean(
                    rawValue,
                    out value);

            default:
                value = null;
                return false;
        }
    }

    private static bool TryParseInteger(string rawValue, out object? value)
    {
        bool parsed = int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result);

        if (!parsed)
        {
            parsed = int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.CurrentCulture, out result);
        }

        value = parsed ? result : null;

        return parsed;
    }

    private static bool TryParseDouble(string rawValue, out object? value)
    {
        bool parsed = double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double result);

        if (!parsed)
        {
            parsed = double.TryParse(rawValue, NumberStyles.Float, CultureInfo.CurrentCulture, out result);
        }

        value = parsed ? result : null;

        return parsed;
    }

    private static bool TryParseBoolean(string rawValue, out object? value)
    {
        bool parsed = bool.TryParse(rawValue, out bool result);

        value = parsed ? result : null;

        return parsed;
    }

    private static CadEntity CreateUnknownEntity(CadBlock block)
    {
        return new CadEntity
        {
            Id = block.Id,

            Type = SemanticEntityType.Unknown,

            Name = block.Name,

            RawName = block.RawName,

            Layer = block.Layer,

            Position = block.Position,

            RotationRadians = block.RotationRadians,

            IsDynamic = block.IsDynamic,

            IsExternalReference = block.IsExternalReference
        };
    }
}
