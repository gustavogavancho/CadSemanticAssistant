using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public async Task<SemanticQuery> InterpretAsync(
        string text,
        InteractionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "The natural-language query cannot be empty.",
                nameof(text));
        }

        ArgumentNullException.ThrowIfNull(context);

        ChatClient client =
            GetClient();

        string systemPrompt = BuildSystemPrompt(context);

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
            context);
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

    private string BuildSystemPrompt(InteractionContext context)
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

            The user may refer to entities currently selected graphically
            in AutoCAD.

            Set useSelection=true only when the user's request explicitly
            refers to the current graphical selection, for example:

            - "these racks"
            - "these objects"
            - "this machine"
            - "the selected racks"
            - "of these"
            - "from the selected elements"

            Do not set useSelection=true merely because objects happen
            to be selected.

            When useSelection=false, the query applies to the whole
            semantic CAD model.

            Do not invent entity IDs.

            The application will resolve the actual selected entity IDs
            deterministically.

            The current graphical selection is provided below.

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
            "useSelection": {
              "type": "boolean"
            }
          },
          "required": [
            "intent",
            "entityType",
            "filters",
            "hasFilters",
            "unsupportedFilters",
            "useSelection"
          ],
          "additionalProperties": false
        }
        """;
    }

    internal SemanticQuery MapToSemanticQuery(
        AiSemanticQueryDto dto,
        InteractionContext context)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(context);

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

        IReadOnlyCollection<string> scopeEntityIds =
            ResolveScopeEntityIds(
                dto,
                entityType,
                context);

        return new SemanticQuery
        {
            Intent =
                intent,

            EntityType =
                entityType,

            Filters =
                filters,

            ScopeEntityIds =
                scopeEntityIds
        };
    }

    private static IReadOnlyCollection<string> ResolveScopeEntityIds(
        AiSemanticQueryDto dto,
        SemanticEntityType entityType,
        InteractionContext context)
    {
        if (!dto.UseSelection)
        {
            return [];
        }

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

        [JsonPropertyName("useSelection")]
        public required bool UseSelection { get; init; }
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
