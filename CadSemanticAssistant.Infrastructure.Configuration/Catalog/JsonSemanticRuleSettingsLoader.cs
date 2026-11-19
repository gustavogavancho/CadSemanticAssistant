using System.Text.Json;
using CadSemanticAssistant.Application.Insertions.Validation;

namespace CadSemanticAssistant.Infrastructure.Configuration.Catalog;

/// <summary>
/// Reads only SEM-005 and SEM-008 numeric thresholds from the versioned rules
/// artifact. Other rules are implemented by the semantic validator or by
/// later geometry/interaction components, not silently interpreted here.
/// </summary>
public static class JsonSemanticRuleSettingsLoader
{
    public static SemanticValidationSettings Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Rules file path is required.", nameof(path));
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetString() != "1.0.0" ||
            root.GetProperty("artifact").GetString() != "domain-rules")
        {
            throw new InvalidDataException("Unsupported domain rules specification.");
        }

        JsonElement rules = root.GetProperty("rules");
        int? min = null;
        int? max = null;
        int? maxComplement = null;
        foreach (JsonElement rule in rules.EnumerateArray())
        {
            string? code = rule.GetProperty("code").GetString();
            if (code == "SEM-005")
            {
                JsonElement parameters = rule.GetProperty("parameters");
                min = parameters.GetProperty("min").GetInt32();
                max = parameters.GetProperty("max").GetInt32();
            }
            else if (code == "SEM-008")
            {
                maxComplement = rule.GetProperty("parameters")
                    .GetProperty("maxPerTypePerParent").GetInt32();
            }
        }

        if (!min.HasValue || !max.HasValue || !maxComplement.HasValue ||
            min < 1 || max < min || maxComplement < 1)
        {
            throw new InvalidDataException("Invalid or missing SEM-005 / SEM-008 limits.");
        }
        return new SemanticValidationSettings
        {
            MinimumInsertions = min.Value,
            MaximumInsertions = max.Value,
            MaximumComplementsPerTypePerParent = maxComplement.Value
        };
    }
}
