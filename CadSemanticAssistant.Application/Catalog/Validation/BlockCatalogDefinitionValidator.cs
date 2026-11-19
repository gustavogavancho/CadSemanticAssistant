using System.Text.RegularExpressions;
using CadSemanticAssistant.Domain.Catalog;

namespace CadSemanticAssistant.Application.Catalog.Validation;

/// <summary>
/// Validates the catalog specification itself. It does NOT validate insertion
/// requests, drawing contents or geometric collisions (subsequent activities).
/// </summary>
public sealed class BlockCatalogDefinitionValidator
{
    public IReadOnlyList<string> Validate(BlockCatalog? catalog)
    {
        List<string> errors = [];

        if (catalog is null)
        {
            errors.Add("Catalog cannot be null.");
            return errors;
        }

        if (!string.Equals(catalog.SchemaVersion, "1.0.0", StringComparison.Ordinal))
        {
            errors.Add($"Unsupported catalog schemaVersion '{catalog.SchemaVersion}'. Expected 1.0.0.");
        }

        if (!string.Equals(catalog.Artifact, "block-catalog", StringComparison.Ordinal))
        {
            errors.Add("Catalog artifact must be 'block-catalog'.");
        }

        ValidateGeometrySettings(catalog.Geometry, errors);
        ValidateIdentity(catalog.InstanceIdentity, errors);

        CatalogBlockDefinition[] items = catalog.CatalogItems ?? [];
        if (items.Length == 0)
        {
            errors.Add("Catalog must contain at least one block definition.");
            return errors;
        }

        HashSet<string> knownIds = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> knownBlockNames = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> machineModels = new(StringComparer.OrdinalIgnoreCase);

        foreach (CatalogBlockDefinition? item in items)
        {
            if (item is null)
            {
                errors.Add("Catalog contains a null item.");
                continue;
            }

            string label = string.IsNullOrWhiteSpace(item.BlockName) ? "(unnamed block)" : item.BlockName;
            if (string.IsNullOrWhiteSpace(item.Id) || !knownIds.Add(item.Id))
            {
                errors.Add($"{label}: missing or duplicate catalog id '{item.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(item.BlockName) || !knownBlockNames.Add(item.BlockName))
            {
                errors.Add($"{label}: missing or duplicate blockName.");
            }

            if (item.SemanticType is not ("Rack" or "Machine" or "Complement"))
            {
                errors.Add($"{label}: unsupported semanticType '{item.SemanticType}'.");
            }

            if (string.IsNullOrWhiteSpace(item.Layer))
            {
                errors.Add($"{label}: layer cannot be empty.");
            }

            ValidateFootprint(item.Footprint, label, errors);
            ValidateAttributes(item, catalog.InstanceIdentity.Attribute, errors);

            if (item.SemanticType == "Machine")
            {
                CatalogAttributeDefinition? model = item.Attributes?.FirstOrDefault(a =>
                    a is not null && string.Equals(a.Name, "MODEL", StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(model?.FixedValue))
                {
                    errors.Add($"{label}: machine MODEL must have a fixedValue.");
                }
                else if (!machineModels.TryAdd(model.FixedValue, label))
                {
                    errors.Add($"{label}: duplicate machine MODEL '{model.FixedValue}'.");
                }
            }
        }

        foreach (CatalogBlockDefinition? item in items)
        {
            if (item is null || item.SemanticType != "Complement")
            {
                continue;
            }

            if (item.AllowedParentModels is not { Length: > 0 })
            {
                errors.Add($"{item.BlockName}: complement must declare allowedParentModels.");
                continue;
            }

            if (item.Attributes is null || !item.Attributes.Any(a =>
                    a is not null && string.Equals(a.Name, "PARENT_CODE", StringComparison.OrdinalIgnoreCase)
                    && a.RequiredOnInsertion && a.ReferenceType == "Machine"))
            {
                errors.Add($"{item.BlockName}: required PARENT_CODE must reference a Machine.");
            }

            foreach (string parentModel in item.AllowedParentModels)
            {
                if (!machineModels.ContainsKey(parentModel))
                {
                    errors.Add($"{item.BlockName}: unknown allowed parent model '{parentModel}'.");
                }
            }
        }

        return errors;
    }

    private static void ValidateGeometrySettings(CatalogGeometry? geometry, List<string> errors)
    {
        if (geometry is null || geometry.CoordinateSystem != "WCS-XY"
            || geometry.Units != "mm" || geometry.InsunitsExpected != 4
            || geometry.RotationUnit != "rad")
        {
            errors.Add("Geometry settings must use WCS-XY, millimeters, INSUNITS=4 and radians.");
        }
        else if (!double.IsFinite(geometry.ToleranceMm) || geometry.ToleranceMm <= 0)
        {
            errors.Add("Geometry toleranceMm must be finite and positive.");
        }
    }

    private static void ValidateIdentity(CatalogInstanceIdentity? identity, List<string> errors)
    {
        if (identity is null || string.IsNullOrWhiteSpace(identity.Attribute)
            || identity.UniqueWithin != "document" || !identity.CaseInsensitive
            || !identity.RequiredOnNewInsertions)
        {
            errors.Add("Instance identity must be required, case-insensitive and unique within the document.");
        }
    }

    private static void ValidateFootprint(CatalogFootprint? footprint, string label, List<string> errors)
    {
        if (footprint is null)
        {
            errors.Add($"{label}: footprint is missing.");
            return;
        }

        if (string.IsNullOrWhiteSpace(footprint.Anchor))
        {
            errors.Add($"{label}: footprint anchor is missing.");
        }

        if (footprint.Kind == "circle")
        {
            if (!IsPoint(footprint.CenterLocalMm) || footprint.RadiusMm is null
                || !double.IsFinite(footprint.RadiusMm.Value) || footprint.RadiusMm <= 0)
            {
                errors.Add($"{label}: circle requires a finite center [x,y] and a positive radiusMm.");
            }
        }
        else if (footprint.Kind == "polygon")
        {
            double[][]? vertices = footprint.VerticesLocalMm;
            if (vertices is not { Length: >= 3 } || vertices.Any(v => !IsPoint(v)))
            {
                errors.Add($"{label}: polygon requires at least three finite 2D vertices.");
                return;
            }

            double twiceArea = 0;
            for (int index = 0; index < vertices.Length; index++)
            {
                double[] first = vertices[index];
                double[] second = vertices[(index + 1) % vertices.Length];
                twiceArea += first[0] * second[1] - second[0] * first[1];
            }

            if (!double.IsFinite(twiceArea) || Math.Abs(twiceArea) <= 0.000001)
            {
                errors.Add($"{label}: polygon area must be non-zero.");
            }
        }
        else
        {
            errors.Add($"{label}: unsupported footprint kind '{footprint.Kind}'.");
        }
    }

    private static bool IsPoint(double[]? point) =>
        point is { Length: 2 } && double.IsFinite(point[0]) && double.IsFinite(point[1]);

    private static void ValidateAttributes(CatalogBlockDefinition item, string instanceAttribute, List<string> errors)
    {
        string label = item.BlockName;
        CatalogAttributeDefinition[] attributes = item.Attributes ?? [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

        foreach (CatalogAttributeDefinition? attribute in attributes)
        {
            if (attribute is null || string.IsNullOrWhiteSpace(attribute.Name))
            {
                errors.Add($"{label}: attribute with missing name.");
                continue;
            }

            if (!names.Add(attribute.Name))
            {
                errors.Add($"{label}: duplicate attribute '{attribute.Name}'.");
            }

            if (attribute.Type is not ("string" or "number" or "integer"))
            {
                errors.Add($"{label}.{attribute.Name}: unsupported type '{attribute.Type}'.");
            }

            if (attribute.Type != "string" &&
                (attribute.Pattern is not null || attribute.AllowedValues is not null || attribute.FixedValue is not null))
            {
                errors.Add($"{label}.{attribute.Name}: numeric attributes cannot have string-only restrictions.");
            }

            if (attribute.Type == "string" && (attribute.Minimum.HasValue || attribute.Maximum.HasValue))
            {
                errors.Add($"{label}.{attribute.Name}: string attribute cannot have numeric bounds.");
            }

            if (attribute.Minimum.HasValue && (!double.IsFinite(attribute.Minimum.Value)))
            {
                errors.Add($"{label}.{attribute.Name}: minimum must be finite.");
            }

            if (attribute.Maximum.HasValue && (!double.IsFinite(attribute.Maximum.Value)))
            {
                errors.Add($"{label}.{attribute.Name}: maximum must be finite.");
            }

            if (attribute.Minimum.HasValue && attribute.Maximum.HasValue
                && attribute.Minimum.Value > attribute.Maximum.Value)
            {
                errors.Add($"{label}.{attribute.Name}: minimum exceeds maximum.");
            }

            if (attribute.Type == "integer" &&
                ((attribute.Minimum.HasValue && attribute.Minimum.Value != Math.Truncate(attribute.Minimum.Value))
                || (attribute.Maximum.HasValue && attribute.Maximum.Value != Math.Truncate(attribute.Maximum.Value))))
            {
                errors.Add($"{label}.{attribute.Name}: integer bounds must be whole numbers.");
            }

            if (attribute.AllowedValues is not null &&
                (attribute.AllowedValues.Length == 0 || attribute.AllowedValues.Any(string.IsNullOrWhiteSpace)
                || attribute.AllowedValues.Distinct(StringComparer.OrdinalIgnoreCase).Count() != attribute.AllowedValues.Length))
            {
                errors.Add($"{label}.{attribute.Name}: invalid or duplicate allowedValues.");
            }

            if (attribute.AllowedValues is not null && attribute.FixedValue is not null
                && !attribute.AllowedValues.Contains(attribute.FixedValue, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add($"{label}.{attribute.Name}: fixedValue is not in allowedValues.");
            }

            if (attribute.Pattern is not null)
            {
                try
                {
                    _ = new Regex(attribute.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
                }
                catch (ArgumentException)
                {
                    errors.Add($"{label}.{attribute.Name}: invalid regular expression.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(instanceAttribute) || !attributes.Any(a =>
                a is not null && string.Equals(a.Name, instanceAttribute, StringComparison.OrdinalIgnoreCase)
                && a.RequiredOnInsertion && a.Unique == "drawing"))
        {
            errors.Add($"{label}: a required, drawing-unique {instanceAttribute} attribute is missing.");
        }
    }
}
