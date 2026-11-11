using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CadSemanticAssistant.Application.Conversation;
using CadSemanticAssistant.Application.Interaction;
using CadSemanticAssistant.Application.NaturalLanguage;
using CadSemanticAssistant.Application.Semantics;
using CadSemanticAssistant.Application.Semantics.Configuration;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Queries;
using CadSemanticAssistant.Domain.Semantics;
using OpenAI.Chat;

namespace CadSemanticAssistant.Infrastructure.AI.OpenAI.NaturalLanguage;

public sealed class OpenAiNaturalLanguageQueryInterpreter
    : INaturalLanguageQueryInterpreter
{
    private readonly string? _apiKey;
    private readonly string? _model;
    private readonly string _reasoningEffort;

    private readonly SemanticMappingConfiguration _configuration;

    private ChatClient? _client;

    internal enum AiSelectionUsage
    {
        None,
        Scope,
        SpatialReference
    }

    internal enum AiSpatialRelation
    {
        None,
        WithinDistance,
        Nearest
    }

    public OpenAiNaturalLanguageQueryInterpreter(
        string? apiKey,
        string? model,
        SemanticMappingConfiguration configuration,
        string? reasoningEffort = null)
    {
        _apiKey = apiKey;
        _model = model;
        _reasoningEffort =
            ParseReasoningEffort(reasoningEffort);

        _configuration =
            configuration
            ?? throw new ArgumentNullException(
                nameof(configuration));
    }

    public Task<SemanticQuery> InterpretAsync(
        string text,
        InteractionContext context,
        CancellationToken cancellationToken = default)
    {
        return InterpretAsync(text, context, ConversationContext.Empty, cancellationToken);
    }

    public async Task<SemanticQuery> InterpretAsync(
        string text,
        InteractionContext context,
        ConversationContext conversationContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "The natural-language query cannot be empty.",
                nameof(text));
        }

        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(conversationContext);

        ChatClient client =
            GetClient();

        string systemPrompt = BuildSystemPrompt(context, conversationContext);

        string jsonSchema =
            BuildJsonSchema();

        List<ChatMessage> messages =
        [
            new SystemChatMessage(
                systemPrompt),

            new UserChatMessage(
                text)
        ];

        ChatCompletionOptions options =
            new()
            {
#pragma warning disable OPENAI001
                ReasoningEffortLevel =
                    _reasoningEffort switch
                    {
                        "low" => ChatReasoningEffortLevel.Low,
                        "medium" => ChatReasoningEffortLevel.Medium,
                        _ => ChatReasoningEffortLevel.High
                    },
#pragma warning restore OPENAI001

                ResponseFormat =
                    ChatResponseFormat.CreateJsonSchemaFormat(
                        jsonSchemaFormatName:
                            "cad_semantic_query",

                        jsonSchema:
                            BinaryData.FromBytes(
                                Encoding.UTF8.GetBytes(
                                    jsonSchema)),

                        jsonSchemaIsStrict:
                            true)
            };

        ChatCompletion completion =
            await client.CompleteChatAsync(
                messages,
                options,
                cancellationToken);

        string? responseText =
            completion.Content
                .FirstOrDefault()
                ?.Text;

        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new InvalidOperationException(
                "The language model returned an empty response.");
        }

        AiSemanticQueryDto? dto =
            JsonSerializer.Deserialize<AiSemanticQueryDto>(
                responseText,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (dto is null)
        {
            throw new InvalidOperationException(
                "The language model response could not be parsed.");
        }

        return MapToSemanticQuery(
            dto,
            context,
            conversationContext);
    }

    private ChatClient GetClient()
    {
        if (_client is not null)
        {
            return _client;
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException(
                "OPENAI_API_KEY is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_model))
        {
            throw new InvalidOperationException(
                "CSA_OPENAI_MODEL is not configured.");
        }

        _client =
            new ChatClient(
                _model,
                _apiKey);

        return _client;
    }

    internal static string ParseReasoningEffort(
        string? reasoningEffort)
    {
        if (string.IsNullOrWhiteSpace(reasoningEffort))
        {
            return "high";
        }

        return reasoningEffort.Trim().ToLowerInvariant() switch
        {
            "low" => "low",
            "medium" => "medium",
            "high" => "high",
            _ => throw new InvalidOperationException(
                "CSA_OPENAI_REASONING_EFFORT must be low, medium, or high.")
        };
    }

    private string BuildSystemPrompt(InteractionContext context, ConversationContext conversationContext)
    {
        StringBuilder builder =
            new();

        builder.AppendLine(
            """
            You convert natural-language CAD queries into
            structured semantic queries.

            You do NOT answer the question.
            You do NOT generate AutoCAD commands.
            You do NOT generate C# code.
            You only translate the user's request.

            Preserve the entity that the user explicitly asks about.
            Never substitute a different entity just because it has
            a requested filter property, and never silently omit that
            filter. Use the canonical target-property name from the
            semantic model when possible. If the property is not mapped
            for the requested entity, keep the requested entity and
            report its original name in unsupportedFilters. Set hasFilters
            to true whenever the user requests any filter, even if none of
            the requested properties can be mapped. Never turn a filtered
            request into an unfiltered query.

            All filters are combined using AND.

            For numeric properties with a unit, convert the
            value expressed by the user into that configured unit.

            Examples:
            - 10 metres -> 10000 when the property unit is mm.
            - 2.5 metres -> 2500 when the property unit is mm.

            Use:
            Select = show/select/find entities
            Count  = count entities
            List   = list/describe entities

            The AutoCAD selection may have three roles:

            selectionUsage="None": the request targets the whole drawing,
            even when some objects happen to be selected.

            selectionUsage="Scope": the GRAPHICALLY selected entities are
            the candidates. Examples: "de estos racks seleccionados",
            "among the currently selected graphical elements".

            selectionUsage="SpatialReference": the single selected entity
            is the reference, not the set of candidates.
            Examples: "racks within 10 metres of this machine",
            "the nearest rack to the selected machine".

            Set hasSpatialConstraint=true for EVERY user-requested spatial
            requirement, even if the requested relation is unsupported.
            Do not omit, replace or approximate a spatial requirement.
            If spatial relations are unsupported or ambiguous, report them
            in unsupportedSpatialConstraints.

            Supported spatial relations:
            WithinDistance requires an explicit numeric distance. Convert
            distance to millimetres (5 m => 5000, 250 cm => 2500).
            The application will measure distances between insertion points
            in the current test drawing, which uses millimetres.
            Nearest is used only when the user asks for the single nearest
            or closest entity; spatialDistance MUST be null.

            Unsupported v0.9 examples: "near this machine" without a
            distance, "inside this area", "left of this machine",
            "between two machines", "intersects this rack".

            Never invent handles, ObjectIds, IDs, coordinates or distances
            measured from CAD geometry. The application resolves handles and
            computes spatial results deterministically.

            Selection does not imply selectionUsage="Scope" or
            selectionUsage="SpatialReference" unless the request requires it.

            Conversational context is DIFFERENT from graphical selection.
            usePreviousResults=true ONLY when the user refers to the last
            executed query's results: "de esos", "of those results",
            "entre los resultados anteriores", "which of those results",
            or a clear follow-up such as "and how many of those are SC2?".
            When previous executed results exist, "de esos" in a follow-up
            means PREVIOUS RESULTS, not current graphical selection, even
            if AutoCAD currently has objects selected. The phrase "de estos
            objetos seleccionados" explicitly means graphical selection.
            Example: last executed query produced 3 Rack results; user says
            "¿Y de esos cuáles son SC1?": entityType=Rack,
            hasFilters=true, filter model Equals SC1,
            selectionUsage=None, hasConversationalReference=true,
            usePreviousResults=true, hasSpatialConstraint=false.
            This previous-result reference must be resolved by the application.
            Never invent previous-result entity IDs.

            Set hasConversationalReference=true whenever the user's request
            refers to a previous result, including when no previous result
            is available. In that case set usePreviousResults=true; the
            application will reject the missing context.
            If a conversational reference is unsupported/ambiguous, place
            its description in unsupportedConversationalReferences.
            Never silently omit a conversational reference or broaden a
            previous-results query into a whole-drawing query.

            For a previous-results follow-up, use the SAME entity type as
            the last executed results, unless the user explicitly asks to
            switch types, which is unsupported in v1.0. Report unsupported
            conversational references rather than substituting another type.
            Apply only NEW property filters from the new utterance. The
            previous results already encode the previous query's restrictions;
            do not duplicate the old filters or old spatial relation.

            v1.0 does not combine previous-results scope with graphical
            selection Scope/SpatialReference, nor with a NEW spatial relation.
            Mark unsupported combinations rather than silently dropping one.

            Semantic CAD model:
            """);

        builder.AppendLine();
        builder.AppendLine(
            "Current graphical selection:");

        if (!context.HasSelection)
        {
            builder.AppendLine(
                "- No supported semantic entities are currently selected.");
        }
        else
        {
            foreach (
                CadEntity entity
                in context.SelectedEntities)
            {
                string properties =
                    string.Join(
                        ", ",
                        entity.Properties.Select(
                            property =>
                                $"{property.Key}={property.Value.Value}"));

                builder.AppendLine(
                    $"- {entity.Type}: {entity.Name}" +
                    (
                        string.IsNullOrWhiteSpace(properties)
                            ? string.Empty
                            : $" ({properties})"
                    ));
            }
        }

        builder.AppendLine();
        builder.AppendLine("Last executed query in this drawing:");
        if (!conversationContext.HasPreviousQuery)
        {
            builder.AppendLine("- None (no previous executed query).");
        }
        else
        {
            SemanticQuery previousQuery = conversationContext.PreviousQuery!;
            builder.AppendLine($"- Target: {previousQuery.EntityType}");
            builder.AppendLine($"- Intent: {previousQuery.Intent}");
            builder.AppendLine($"- Result count: {conversationContext.PreviousResultEntityIds.Count}");
            builder.AppendLine("- Filters: " +
                (previousQuery.Filters.Count == 0 ? "none" :
                 string.Join(" AND ", previousQuery.Filters.Select(f =>
                     $"{f.Property} {f.Operator} {f.Value}"))));
            builder.AppendLine("- Spatial relation: " +
                (previousQuery.SpatialFilter?.Relation.ToString() ?? "none"));
            // Do not send handles/ObjectIds from previous results to the LLM.
        }

        foreach (
            EntityMappingRule entityMapping
            in _configuration.EntityMappings)
        {
            builder.AppendLine();
            builder.AppendLine(
                $"Entity: {entityMapping.EntityType}");

            foreach (
                PropertyMappingRule property
                in entityMapping.Properties)
            {
                string unit =
                    string.IsNullOrWhiteSpace(
                        property.Unit)
                        ? string.Empty
                        : $", unit={property.Unit}";

                builder.AppendLine(
                    $"- {property.TargetProperty}: " +
                    $"{property.DataType}{unit}");
            }
        }

        return builder.ToString();
    }

    internal string BuildJsonSchema()
    {
        string entityTypes =
            string.Join(
                ",",
                _configuration
                    .EntityMappings
                    .Select(mapping =>
                        mapping.EntityType.ToString())
                    .Distinct()
                    .Select(entityType =>
                        JsonSerializer.Serialize(entityType)));

        string propertyNames =
            string.Join(
                ",",
                _configuration.EntityMappings
                    .SelectMany(mapping => mapping.Properties)
                    .Select(property => property.TargetProperty)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(propertyName =>
                        JsonSerializer.Serialize(propertyName)));

        string operators =
            string.Join(
                ",",
                Enum.GetNames<QueryOperator>()
                    .Select(@operator =>
                        JsonSerializer.Serialize(@operator)));

        return $$"""
        {
          "type": "object",
          "properties": {
            "intent": {
              "type": "string",
              "enum": [
                "Select",
                "Count",
                "List"
              ]
            },
            "entityType": {
              "type": "string",
              "enum": [
                {{entityTypes}}
              ]
            },
            "filters": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "property": {
                    "type": "string",
                    "enum": [
                      {{propertyNames}}
                    ]
                  },
                  "operator": {
                    "type": "string",
                    "enum": [
                      {{operators}}
                    ]
                  },
                  "value": {
                    "type": "string"
                  }
                },
                "required": [
                  "property",
                  "operator",
                  "value"
                ],
                "additionalProperties": false
              }
            },
            "hasFilters": {
              "type": "boolean"
            },
            "unsupportedFilters": {
              "type": "array",
              "items": {
                "type": "string"
              }
            },
            "selectionUsage": {
              "type": "string",
              "enum": ["None", "Scope", "SpatialReference"]
            },
            "hasSpatialConstraint": {
              "type": "boolean"
            },
            "spatialRelation": {
              "type": "string",
              "enum": ["None", "WithinDistance", "Nearest"]
            },
            "spatialDistance": {
              "type": ["number", "null"]
            },
            "unsupportedSpatialConstraints": {
              "type": "array",
              "items": { "type": "string" }
            },
            "hasConversationalReference": {
              "type": "boolean"
            },
            "usePreviousResults": {
              "type": "boolean"
            },
            "unsupportedConversationalReferences": {
              "type": "array",
              "items": { "type": "string" }
            }
          },
          "required": [
            "intent",
            "entityType",
            "filters",
            "hasFilters",
            "unsupportedFilters",
            "selectionUsage",
            "hasSpatialConstraint",
            "spatialRelation",
            "spatialDistance",
            "unsupportedSpatialConstraints",
            "hasConversationalReference",
            "usePreviousResults",
            "unsupportedConversationalReferences"
          ],
          "additionalProperties": false
        }
        """;
    }

    internal SemanticQuery MapToSemanticQuery(
        AiSemanticQueryDto dto,
        InteractionContext context)
    {
        return MapToSemanticQuery(dto, context, ConversationContext.Empty);
    }

    internal SemanticQuery MapToSemanticQuery(
        AiSemanticQueryDto dto,
        InteractionContext context,
        ConversationContext conversationContext)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(conversationContext);

        if (!Enum.TryParse(
                dto.Intent,
                true,
                out QueryIntent intent))
        {
            throw new InvalidOperationException(
                $"Unknown query intent '{dto.Intent}'.");
        }

        if (!Enum.TryParse(
                dto.EntityType,
                true,
                out SemanticEntityType entityType))
        {
            throw new InvalidOperationException(
                $"Unknown entity type '{dto.EntityType}'.");
        }

        EntityMappingRule? entityMapping =
            _configuration.EntityMappings
                .FirstOrDefault(
                    mapping =>
                        mapping.EntityType ==
                        entityType);

        if (entityMapping is null)
        {
            throw new InvalidOperationException(
                $"Entity type '{entityType}' is not configured.");
        }

        if (dto.UnsupportedFilters.Length > 0)
        {
            throw new InvalidOperationException(
                "Unsupported filter property or properties: " +
                $"{string.Join(", ", dto.UnsupportedFilters)}.");
        }

        if (dto.HasFilters && dto.Filters.Length == 0)
        {
            throw new InvalidOperationException(
                "The request contains filters, but none could be mapped " +
                "to supported properties.");
        }

        if (!dto.HasFilters && dto.Filters.Length > 0)
        {
            throw new InvalidOperationException(
                "The interpreter returned filters without identifying " +
                "a filter request.");
        }

        if (!Enum.TryParse(dto.SelectionUsage, true,
                out AiSelectionUsage selectionUsage) ||
            !Enum.IsDefined(selectionUsage))
        {
            throw new InvalidOperationException(
                $"Unknown selection usage '{dto.SelectionUsage}'.");
        }

        if (!Enum.TryParse(dto.SpatialRelation, true,
                out AiSpatialRelation spatialRelation) ||
            !Enum.IsDefined(spatialRelation))
        {
            throw new InvalidOperationException(
                $"Unknown spatial relation '{dto.SpatialRelation}'.");
        }

        ValidateSpatialInterpretation(dto, selectionUsage, spatialRelation);
        ValidateConversationInterpretation(dto, selectionUsage, conversationContext);

        List<QueryFilter> filters =
            [];

        foreach (
            AiQueryFilterDto filterDto
            in dto.Filters)
        {
            PropertyMappingRule? propertyMapping =
                entityMapping.Properties
                    .FirstOrDefault(
                        property =>
                            string.Equals(
                                property.TargetProperty,
                                filterDto.Property,
                                StringComparison.OrdinalIgnoreCase));

            if (propertyMapping is null)
            {
                throw new InvalidOperationException(
                    $"Property '{filterDto.Property}' " +
                    $"is not valid for entity '{entityType}'.");
            }

            if (!Enum.TryParse(
                    filterDto.Operator,
                    true,
                    out QueryOperator queryOperator))
            {
                throw new InvalidOperationException(
                    $"Unknown operator '{filterDto.Operator}'.");
            }

            object value =
                ConvertValue(
                    filterDto.Value,
                    propertyMapping.DataType);

            filters.Add(
                new QueryFilter
                {
                    Property =
                        propertyMapping.TargetProperty,

                    Operator =
                        queryOperator,

                    Value =
                        value
                });
        }

        IReadOnlyCollection<string> scopeEntityIds = [];
        QueryScopeSource scopeSource = QueryScopeSource.None;
        SpatialFilter? spatialFilter = null;

        if (dto.UsePreviousResults)
        {
            if (conversationContext.PreviousQuery!.EntityType != entityType)
            {
                throw new InvalidOperationException(
                    "Previous-results queries must use the entity type " +
                    "returned by the last executed query.");
            }

            scopeEntityIds = conversationContext.PreviousResultEntityIds.ToArray();
            scopeSource = QueryScopeSource.PreviousResults;
        }
        else
        {
            switch (selectionUsage)
            {
                case AiSelectionUsage.None:
                    break;

                case AiSelectionUsage.Scope:
                    scopeEntityIds = ResolveScopeEntityIds(entityType, context);
                    scopeSource = QueryScopeSource.GraphicalSelection;
                    break;

                case AiSelectionUsage.SpatialReference:
                    spatialFilter = ResolveSpatialFilter(dto, spatialRelation, context);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported selection usage '{selectionUsage}'.");
            }
        }

        return new SemanticQuery
        {
            Intent =
                intent,

            EntityType =
                entityType,

            Filters =
                filters,

            ScopeEntityIds =
                scopeEntityIds,

            ScopeSource = scopeSource,

            SpatialFilter =
                spatialFilter
        };
    }

    private static void ValidateConversationInterpretation(
        AiSemanticQueryDto dto,
        AiSelectionUsage selectionUsage,
        ConversationContext conversationContext)
    {
        if (dto.UnsupportedConversationalReferences.Length > 0)
        {
            throw new InvalidOperationException(
                "Unsupported conversational reference: " +
                string.Join(", ", dto.UnsupportedConversationalReferences));
        }

        if (dto.HasConversationalReference != dto.UsePreviousResults)
        {
            throw new InvalidOperationException(
                "The conversational reference was omitted or is inconsistent.");
        }

        if (!dto.UsePreviousResults)
        {
            return;
        }

        if (selectionUsage != AiSelectionUsage.None || dto.HasSpatialConstraint)
        {
            throw new InvalidOperationException(
                "Combining previous results with a new graphical/spatial " +
                "selection is not supported in v1.0.");
        }

        if (!conversationContext.HasPreviousQuery)
        {
            throw new InvalidOperationException(
                "The query refers to previous results, but no query has been executed in this drawing.");
        }

        if (!conversationContext.HasPreviousResults)
        {
            throw new InvalidOperationException(
                "The previous query returned zero entities. Previous-results scope cannot be empty.");
        }
    }

    private static IReadOnlyCollection<string> ResolveScopeEntityIds(
        SemanticEntityType entityType,
        InteractionContext context)
    {
        if (!context.HasSelection)
        {
            throw new InvalidOperationException(
                "The query refers to the current AutoCAD selection, " +
                "but no supported semantic entities are selected.");
        }

        CadEntity[] matchingSelectedEntities =
            context.SelectedEntities
                .Where(
                    entity =>
                        entity.Type ==
                        entityType)
                .ToArray();

        if (matchingSelectedEntities.Length == 0)
        {
            throw new InvalidOperationException(
                $"The query refers to selected '{entityType}' entities, " +
                "but the current AutoCAD selection contains none.");
        }

        return matchingSelectedEntities
            .Select(
                entity =>
                    entity.Id)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void ValidateSpatialInterpretation(
        AiSemanticQueryDto dto,
        AiSelectionUsage selectionUsage,
        AiSpatialRelation spatialRelation)
    {
        if (dto.UnsupportedSpatialConstraints.Length > 0)
        {
            throw new InvalidOperationException(
                "Unsupported spatial constraint or constraints: " +
                $"{string.Join(", ", dto.UnsupportedSpatialConstraints)}.");
        }

        if (dto.HasSpatialConstraint)
        {
            if (selectionUsage != AiSelectionUsage.SpatialReference)
            {
                throw new InvalidOperationException(
                    "A spatial request must use the graphical selection " +
                    "as a spatial reference.");
            }

            if (spatialRelation == AiSpatialRelation.None)
            {
                throw new InvalidOperationException(
                    "The request contains a spatial constraint, " +
                    "but no supported spatial relation was returned.");
            }

            return;
        }

        if (selectionUsage == AiSelectionUsage.SpatialReference ||
            spatialRelation != AiSpatialRelation.None ||
            dto.SpatialDistance is not null)
        {
            throw new InvalidOperationException(
                "Spatial data was returned without identifying a " +
                "spatial request.");
        }
    }

    private static SpatialFilter ResolveSpatialFilter(
        AiSemanticQueryDto dto,
        AiSpatialRelation relation,
        InteractionContext context)
    {
        if (context.SelectedEntities.Count != 1)
        {
            throw new InvalidOperationException(
                "Spatial queries currently require exactly one " +
                "selected semantic reference entity.");
        }

        CadEntity selected = context.SelectedEntities.Single();

        switch (relation)
        {
            case AiSpatialRelation.WithinDistance:
                if (dto.SpatialDistance is not double distance ||
                    !double.IsFinite(distance) || distance <= 0)
                {
                    throw new InvalidOperationException(
                        "WithinDistance requires a finite, positive distance.");
                }

                return new SpatialFilter
                {
                    Relation = SpatialRelation.WithinDistance,
                    ReferenceEntityIds = [selected.Id],
                    MaxDistance = distance
                };

            case AiSpatialRelation.Nearest:
                if (dto.SpatialDistance is not null)
                {
                    throw new InvalidOperationException(
                        "Nearest must not define a spatial distance.");
                }

                return new SpatialFilter
                {
                    Relation = SpatialRelation.Nearest,
                    ReferenceEntityIds = [selected.Id],
                    MaxDistance = null
                };

            default:
                throw new InvalidOperationException(
                    $"Spatial relation '{relation}' is not supported.");
        }
    }

    private static object ConvertValue(
        string value,
        SemanticDataType dataType)
    {
        return dataType switch
        {
            SemanticDataType.String =>
                value,

            SemanticDataType.Integer =>
                int.Parse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture),

            SemanticDataType.Double =>
                double.Parse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture),

            SemanticDataType.Boolean =>
                bool.Parse(
                    value),

            _ =>
                throw new InvalidOperationException(
                    $"Unsupported semantic data type '{dataType}'.")
        };
    }

    internal sealed class AiSemanticQueryDto
    {
        [JsonPropertyName("intent")]
        public required string Intent { get; init; }

        [JsonPropertyName("entityType")]
        public required string EntityType { get; init; }

        [JsonPropertyName("filters")]
        public AiQueryFilterDto[] Filters { get; init; }
            = [];

        [JsonPropertyName("hasFilters")]
        public required bool HasFilters { get; init; }

        [JsonPropertyName("unsupportedFilters")]
        public string[] UnsupportedFilters { get; init; }
            = [];

        [JsonPropertyName("selectionUsage")]
        public string SelectionUsage { get; init; }
            = nameof(AiSelectionUsage.None);

        [JsonPropertyName("hasSpatialConstraint")]
        public bool HasSpatialConstraint { get; init; }

        [JsonPropertyName("spatialRelation")]
        public string SpatialRelation { get; init; }
            = nameof(AiSpatialRelation.None);

        [JsonPropertyName("spatialDistance")]
        public double? SpatialDistance { get; init; }

        [JsonPropertyName("unsupportedSpatialConstraints")]
        public string[] UnsupportedSpatialConstraints { get; init; }
            = [];

        [JsonPropertyName("hasConversationalReference")]
        public bool HasConversationalReference { get; init; }

        [JsonPropertyName("usePreviousResults")]
        public bool UsePreviousResults { get; init; }

        [JsonPropertyName("unsupportedConversationalReferences")]
        public string[] UnsupportedConversationalReferences { get; init; } = [];
    }

    internal sealed class AiQueryFilterDto
    {
        [JsonPropertyName("property")]
        public required string Property { get; init; }

        [JsonPropertyName("operator")]
        public required string Operator { get; init; }

        [JsonPropertyName("value")]
        public required string Value { get; init; }
    }
}
