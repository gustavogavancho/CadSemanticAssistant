using System.Globalization;
using System.Text.RegularExpressions;
using CadSemanticAssistant.Application.Catalog;
using CadSemanticAssistant.Domain.Catalog;
using CadSemanticAssistant.Domain.Insertions;

namespace CadSemanticAssistant.Application.Insertions.Validation;

/// <summary>
/// Semantic rules SEM-001..SEM-008 for explicit insertion instances.
/// No OpenAI dependency, no AutoCAD dependency, no geometry checks or writes.
/// The caller must supply an up-to-date snapshot of existing references.
/// </summary>
public sealed class BlockInsertionSemanticValidator
{
    private readonly IBlockCatalogProvider _catalogProvider;
    private readonly SemanticValidationSettings _settings;

    public BlockInsertionSemanticValidator(
        IBlockCatalogProvider catalogProvider,
        SemanticValidationSettings? settings = null)
    {
        _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        _settings = settings ?? new SemanticValidationSettings();
        if (_settings.MinimumInsertions < 1 ||
            _settings.MaximumInsertions < _settings.MinimumInsertions ||
            _settings.MaximumComplementsPerTypePerParent < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Invalid SEM-005/SEM-008 settings.");
        }
    }

    public SemanticValidationResult Validate(BlockInsertionBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        BlockCatalog catalog = _catalogProvider.GetCatalog();
        IReadOnlyList<BlockInsertionDraft> proposed = batch.Proposed ?? Array.Empty<BlockInsertionDraft>();
        IReadOnlyList<ExistingBlockSnapshot> existing = batch.Existing ?? Array.Empty<ExistingBlockSnapshot>();
        List<SemanticValidationIssue> issues = [];

        if (proposed.Count < _settings.MinimumInsertions ||
            proposed.Count > _settings.MaximumInsertions)
        {
            issues.Add(new SemanticValidationIssue("SEM-005",
                $"Insertion quantity {proposed.Count} is outside " +
                $"[{_settings.MinimumInsertions}, {_settings.MaximumInsertions}]."));
        }

        // Only defined, enabled catalog blocks can be new insertions (SEM-001).
        CatalogBlockDefinition?[] definitions = new CatalogBlockDefinition?[proposed.Count];
        Dictionary<string, string?>[] proposedAttributes = new Dictionary<string, string?>[proposed.Count];
        for (int i = 0; i < proposed.Count; i++)
        {
            BlockInsertionDraft? draft = proposed[i];
            if (draft is null)
            {
                proposedAttributes[i] = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                issues.Add(new SemanticValidationIssue("SEM-001", "Null insertion proposal.", i));
                continue;
            }

            proposedAttributes[i] = NormalizeAttributes(draft.Attributes, issues, i);
            CatalogBlockDefinition? definition = catalog.FindByBlockName(draft.BlockName);
            if (definition is null || !definition.InsertionEnabled)
            {
                issues.Add(new SemanticValidationIssue("SEM-001",
                    $"Block '{draft.BlockName}' is unknown or insertion is disabled.", i));
                continue;
            }

            definitions[i] = definition;
            ValidateAttributes(definition, proposedAttributes[i], issues, i);
        }

        // The identity is unique in the drawing, including the entire batch
        // (SEM-004). Existing references without a code remain queryable.
        string identityName = catalog.InstanceIdentity.Attribute;
        Dictionary<string, string> identityOwners = new(StringComparer.OrdinalIgnoreCase);
        foreach (ExistingBlockSnapshot? item in existing)
        {
            if (item is null)
            {
                continue;
            }
            string? code = Get(item.Attributes, identityName);
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }
            string normalized = code.Trim();
            if (!identityOwners.TryAdd(normalized, "existing drawing"))
            {
                issues.Add(new SemanticValidationIssue("SEM-004",
                    $"Duplicate existing INSTANCE_CODE '{normalized}' in drawing."));
            }
        }

        for (int i = 0; i < proposed.Count; i++)
        {
            string? code = Get(proposedAttributes[i], identityName);
            if (string.IsNullOrWhiteSpace(code))
            {
                continue; // SEM-002 already covers this
            }
            string normalized = code.Trim();
            if (!identityOwners.TryAdd(normalized, $"proposed insertion #{i + 1}"))
            {
                issues.Add(new SemanticValidationIssue("SEM-004",
                    $"INSTANCE_CODE '{normalized}' already exists ({identityOwners[normalized]}).",
                    i, identityName));
            }
        }

        // Index all codes to avoid silently choosing a parent when codes are
        // ambiguous. Compatibility is checked against the actual MODEL value.
        Dictionary<string, List<ParentCandidate>> possibleParents =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (ExistingBlockSnapshot? item in existing)
        {
            if (item is null)
            {
                continue;
            }
            AddCandidate(catalog, possibleParents, item.BlockName, item.Attributes, identityName);
        }
        for (int i = 0; i < proposed.Count; i++)
        {
            if (proposed[i] is null)
            {
                continue;
            }
            AddCandidate(catalog, possibleParents, proposed[i].BlockName,
                proposedAttributes[i], identityName);
        }

        for (int i = 0; i < proposed.Count; i++)
        {
            CatalogBlockDefinition? definition = definitions[i];
            if (definition?.SemanticType != "Complement")
            {
                continue;
            }

            string? parentCode = Get(proposedAttributes[i], "PARENT_CODE")?.Trim();
            if (string.IsNullOrWhiteSpace(parentCode))
            {
                continue; // SEM-002
            }

            if (!possibleParents.TryGetValue(parentCode, out List<ParentCandidate>? candidates) ||
                candidates.Count != 1 || !candidates[0].IsValidMachine)
            {
                issues.Add(new SemanticValidationIssue("SEM-006",
                    $"PARENT_CODE '{parentCode}' must identify exactly one valid machine.",
                    i, "PARENT_CODE"));
                continue;
            }

            string? model = candidates[0].MachineModel;
            if (model is null || !definition.AllowedParentModels.Contains(model,
                    StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(new SemanticValidationIssue("SEM-007",
                    $"{definition.BlockName} cannot be attached to machine model '{model}'.",
                    i, "PARENT_CODE"));
            }
        }

        // SEM-008 includes installed complements and all proposed complements.
        Dictionary<(string BlockName, string ParentCode), int> complementCounts =
            new(new ComplementKeyComparer());
        foreach (ExistingBlockSnapshot? item in existing)
        {
            if (item is null)
            {
                continue;
            }
            AddComplement(catalog, complementCounts, item.BlockName, item.Attributes);
        }
        foreach (KeyValuePair<(string BlockName, string ParentCode), int> count in complementCounts)
        {
            if (count.Value > _settings.MaximumComplementsPerTypePerParent)
            {
                issues.Add(new SemanticValidationIssue("SEM-008",
                    $"Drawing already exceeds the maximum of '{count.Key.BlockName}' " +
                    $"for machine '{count.Key.ParentCode}'."));
            }
        }
        for (int i = 0; i < proposed.Count; i++)
        {
            if (definitions[i]?.SemanticType != "Complement")
            {
                continue;
            }
            string? parentCode = Get(proposedAttributes[i], "PARENT_CODE")?.Trim();
            if (string.IsNullOrWhiteSpace(parentCode))
            {
                continue;
            }
            (string, string) key = (definitions[i]!.BlockName, parentCode);
            complementCounts.TryGetValue(key, out int count);
            count++;
            complementCounts[key] = count;
            if (count > _settings.MaximumComplementsPerTypePerParent)
            {
                issues.Add(new SemanticValidationIssue("SEM-008",
                    $"Machine '{parentCode}' already has the maximum number of '{definitions[i]!.BlockName}'.",
                    i, "PARENT_CODE"));
            }
        }
        return new SemanticValidationResult(issues);
    }

    private static Dictionary<string, string?> NormalizeAttributes(
        IReadOnlyDictionary<string, string?>? values,
        List<SemanticValidationIssue> issues, int proposedIndex)
    {
        Dictionary<string, string?> normalized = new(StringComparer.OrdinalIgnoreCase);
        if (values is null)
        {
            return normalized;
        }
        foreach (KeyValuePair<string, string?> entry in values)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || !normalized.TryAdd(entry.Key, entry.Value))
            {
                issues.Add(new SemanticValidationIssue("SEM-003",
                    $"Duplicate or blank attribute name '{entry.Key}'.", proposedIndex, entry.Key));
            }
        }
        return normalized;
    }

    private static void ValidateAttributes(
        CatalogBlockDefinition definition, Dictionary<string, string?> attributes,
        List<SemanticValidationIssue> issues, int proposedIndex)
    {
        Dictionary<string, CatalogAttributeDefinition> known = definition.Attributes.ToDictionary(
            attribute => attribute.Name, StringComparer.OrdinalIgnoreCase);
        foreach (string attributeName in attributes.Keys)
        {
            if (!known.ContainsKey(attributeName))
            {
                issues.Add(new SemanticValidationIssue("SEM-003",
                    $"Unknown attribute '{attributeName}' for {definition.BlockName}.",
                    proposedIndex, attributeName));
            }
        }

        foreach (CatalogAttributeDefinition attribute in definition.Attributes)
        {
            bool found = attributes.TryGetValue(attribute.Name, out string? raw);
            if (!found || string.IsNullOrWhiteSpace(raw))
            {
                if (attribute.RequiredOnInsertion)
                {
                    issues.Add(new SemanticValidationIssue("SEM-002",
                        $"Required attribute '{attribute.Name}' is missing or blank.",
                        proposedIndex, attribute.Name));
                }
                continue;
            }

            string value = raw.Trim();
            if (attribute.Type == "string")
            {
                if (attribute.FixedValue is not null &&
                    !string.Equals(value, attribute.FixedValue, StringComparison.Ordinal))
                {
                    issues.Add(new SemanticValidationIssue("SEM-003",
                        $"'{attribute.Name}' must equal '{attribute.FixedValue}'.",
                        proposedIndex, attribute.Name));
                }
                if (attribute.AllowedValues is { Length: > 0 } &&
                    !attribute.AllowedValues.Contains(value, StringComparer.Ordinal))
                {
                    issues.Add(new SemanticValidationIssue("SEM-003",
                        $"'{attribute.Name}' is not an allowed value.",
                        proposedIndex, attribute.Name));
                }
                if (attribute.Pattern is not null &&
                    !Regex.IsMatch(value, attribute.Pattern, RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(200)))
                {
                    issues.Add(new SemanticValidationIssue("SEM-003",
                        $"'{attribute.Name}' does not match the required pattern.",
                        proposedIndex, attribute.Name));
                }
                continue;
            }

            bool validNumber = attribute.Type == "integer"
                ? long.TryParse(value, NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out _)
                : double.TryParse(value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out _);
            if (!validNumber)
            {
                issues.Add(new SemanticValidationIssue("SEM-002",
                    $"'{attribute.Name}' has an invalid {attribute.Type} value.",
                    proposedIndex, attribute.Name));
                continue;
            }

            // Re-parse as double to apply the same inclusive min/max semantics
            // to integral and floating-point numbers.
            if (!double.TryParse(value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double number) || !double.IsFinite(number))
            {
                issues.Add(new SemanticValidationIssue("SEM-002",
                    $"'{attribute.Name}' must be a finite number.", proposedIndex, attribute.Name));
                continue;
            }
            if (attribute.Minimum.HasValue && number < attribute.Minimum.Value ||
                attribute.Maximum.HasValue && number > attribute.Maximum.Value)
            {
                issues.Add(new SemanticValidationIssue("SEM-003",
                    $"'{attribute.Name}' is outside its permitted range.",
                    proposedIndex, attribute.Name));
            }
        }
    }

    private static void AddCandidate(
        BlockCatalog catalog, Dictionary<string, List<ParentCandidate>> candidates,
        string blockName, IReadOnlyDictionary<string, string?>? attributes, string identityName)
    {
        string? code = Get(attributes, identityName)?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        CatalogBlockDefinition? definition = catalog.FindByBlockName(blockName);
        string? actualModel = Get(attributes, "MODEL")?.Trim();
        string? expectedModel = definition?.Attributes.FirstOrDefault(attribute =>
            string.Equals(attribute.Name, "MODEL", StringComparison.OrdinalIgnoreCase))?.FixedValue;
        bool validMachine = definition?.SemanticType == "Machine" &&
            !string.IsNullOrWhiteSpace(expectedModel) &&
            string.Equals(actualModel, expectedModel, StringComparison.Ordinal);

        if (!candidates.TryGetValue(code, out List<ParentCandidate>? matches))
        {
            matches = [];
            candidates.Add(code, matches);
        }
        matches.Add(new ParentCandidate(validMachine, validMachine ? actualModel : null));
    }

    private static void AddComplement(
        BlockCatalog catalog, Dictionary<(string, string), int> counts,
        string blockName, IReadOnlyDictionary<string, string?>? attributes)
    {
        CatalogBlockDefinition? definition = catalog.FindByBlockName(blockName);
        if (definition?.SemanticType != "Complement")
        {
            return;
        }
        string? parentCode = Get(attributes, "PARENT_CODE")?.Trim();
        if (string.IsNullOrWhiteSpace(parentCode))
        {
            return;
        }
        (string, string) key = (definition.BlockName, parentCode);
        counts.TryGetValue(key, out int count);
        counts[key] = count + 1;
    }

    private static string? Get(IReadOnlyDictionary<string, string?>? attributes, string name)
    {
        if (attributes is null)
        {
            return null;
        }
        // Handle dictionaries with case-sensitive keys from external adapters.
        foreach (KeyValuePair<string, string?> attribute in attributes)
        {
            if (string.Equals(attribute.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return attribute.Value;
            }
        }
        return null;
    }

    private sealed record ParentCandidate(bool IsValidMachine, string? MachineModel);

    private sealed class ComplementKeyComparer : IEqualityComparer<(string, string)>
    {
        public bool Equals((string, string) x, (string, string) y) =>
            string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string, string) item) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(item.Item1),
            StringComparer.OrdinalIgnoreCase.GetHashCode(item.Item2));
    }
}
