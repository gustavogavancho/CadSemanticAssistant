using System.Text.Json;

using CadSemanticAssistant.Application.Interaction;
using CadSemanticAssistant.Application.Semantics.Configuration;
using CadSemanticAssistant.Application.Semantics;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Queries;
using CadSemanticAssistant.Domain.Semantics;
using CadSemanticAssistant.Infrastructure.AI.OpenAI.NaturalLanguage;

namespace CadSemanticAssistant.Tests.NaturalLanguage;

public sealed class OpenAiNaturalLanguageQueryInterpreterTests
{
    [Fact]
    public void MapToSemanticQuery_MachineCapacityFilter_ShouldRejectUnsupportedProperty()
    {
        SemanticMappingConfiguration configuration = new()
        {
            EntityMappings =
            [
                new EntityMappingRule
                {
                    EntityType = SemanticEntityType.Machine,
                    Properties =
                    [
                        new PropertyMappingRule
                        {
                            SourceAttribute = "MODEL",
                            TargetProperty = "model",
                            DataType = SemanticDataType.String
                        },
                        new PropertyMappingRule
                        {
                            SourceAttribute = "TYPE",
                            TargetProperty = "machineType",
                            DataType = SemanticDataType.String
                        }
                    ]
                },
                new EntityMappingRule
                {
                    EntityType = SemanticEntityType.Rack,
                    Properties =
                    [
                        new PropertyMappingRule
                        {
                            SourceAttribute = "CAPACITY",
                            TargetProperty = "capacity",
                            DataType = SemanticDataType.Integer
                        }
                    ]
                }
            ]
        };

        OpenAiNaturalLanguageQueryInterpreter interpreter =
            new(null, null, configuration);

        OpenAiNaturalLanguageQueryInterpreter.AiSemanticQueryDto dto = new()
        {
            Intent = "Select",
            EntityType = "Machine",
            HasFilters = true,
            Filters =
            [
                new OpenAiNaturalLanguageQueryInterpreter.AiQueryFilterDto
                {
                    Property = "capacity",
                    Operator = nameof(QueryOperator.GreaterThan),
                    Value = "20"
                }
            ],
            UnsupportedFilters = [],
            UseSelection = false
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => interpreter.MapToSemanticQuery(dto, new InteractionContext()));

        Assert.Equal(
            "Property 'capacity' is not valid for entity 'Machine'.",
            exception.Message);
    }

    [Fact]
    public void MapToSemanticQuery_UnsupportedFilter_ShouldRejectInsteadOfReturningUnfilteredQuery()
    {
        OpenAiNaturalLanguageQueryInterpreter interpreter =
            CreateRackInterpreter();

        OpenAiNaturalLanguageQueryInterpreter.AiSemanticQueryDto dto = new()
        {
            Intent = "Select",
            EntityType = "Rack",
            HasFilters = true,
            Filters = [],
            UnsupportedFilters = ["weight"],
            UseSelection = false
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => interpreter.MapToSemanticQuery(dto, new InteractionContext()));

        Assert.Equal(
            "Unsupported filter property or properties: weight.",
            exception.Message);
    }

    [Fact]
    public void MapToSemanticQuery_FilterRequestedButOmitted_ShouldRejectInsteadOfReturningUnfilteredQuery()
    {
        OpenAiNaturalLanguageQueryInterpreter interpreter =
            CreateRackInterpreter();

        OpenAiNaturalLanguageQueryInterpreter.AiSemanticQueryDto dto = new()
        {
            Intent = "Select",
            EntityType = "Rack",
            HasFilters = true,
            Filters = [],
            UnsupportedFilters = [],
            UseSelection = false
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => interpreter.MapToSemanticQuery(dto, new InteractionContext()));

        Assert.Equal(
            "The request contains filters, but none could be mapped to supported properties.",
            exception.Message);
    }

    [Fact]
    public void BuildJsonSchema_RequiresFilterIntentAndUnsupportedFilters()
    {
        OpenAiNaturalLanguageQueryInterpreter interpreter =
            CreateRackInterpreter();

        using JsonDocument schema =
            JsonDocument.Parse(interpreter.BuildJsonSchema());

        string[] required = schema.RootElement
            .GetProperty("required")
            .EnumerateArray()
            .Select(property => property.GetString() ?? string.Empty)
            .ToArray();

        Assert.Contains("hasFilters", required);
        Assert.Contains("unsupportedFilters", required);
        Assert.Contains("useSelection", required);
    }

    [Fact]
    public void MapToSemanticQuery_UseSelection_ShouldScopeToMatchingSelectedEntityIds()
    {
        OpenAiNaturalLanguageQueryInterpreter interpreter =
            CreateRackInterpreter();

        OpenAiNaturalLanguageQueryInterpreter.AiSemanticQueryDto dto = new()
        {
            Intent = "Select",
            EntityType = "Rack",
            HasFilters = false,
            Filters = [],
            UnsupportedFilters = [],
            UseSelection = true
        };

        InteractionContext context = new()
        {
            SelectedEntities =
            [
                CreateEntity("rack-1", SemanticEntityType.Rack),
                CreateEntity("rack-2", SemanticEntityType.Rack),
                CreateEntity("machine-1", SemanticEntityType.Machine)
            ]
        };

        SemanticQuery query =
            interpreter.MapToSemanticQuery(dto, context);

        Assert.Equal(
            new[] { "rack-1", "rack-2" },
            query.ScopeEntityIds);
    }

    [Fact]
    public void MapToSemanticQuery_UseSelectionWithoutMatchingEntities_ShouldThrow()
    {
        OpenAiNaturalLanguageQueryInterpreter interpreter =
            CreateRackInterpreter();

        OpenAiNaturalLanguageQueryInterpreter.AiSemanticQueryDto dto = new()
        {
            Intent = "Select",
            EntityType = "Rack",
            HasFilters = false,
            Filters = [],
            UnsupportedFilters = [],
            UseSelection = true
        };

        InteractionContext context = new()
        {
            SelectedEntities =
            [
                CreateEntity("machine-1", SemanticEntityType.Machine)
            ]
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => interpreter.MapToSemanticQuery(dto, context));

        Assert.Contains("contains none", exception.Message);
    }

    [Fact]
    public void ParseReasoningEffort_WhenUnset_ShouldDefaultToHigh()
    {
        Assert.Equal(
            "high",
            OpenAiNaturalLanguageQueryInterpreter.ParseReasoningEffort(null));
    }

    [Theory]
    [InlineData("low", "low")]
    [InlineData("medium", "medium")]
    [InlineData("high", "high")]
    [InlineData(" HIGH ", "high")]
    public void ParseReasoningEffort_ShouldAcceptSupportedValues(
        string effort,
        string expected)
    {
        Assert.Equal(
            expected,
            OpenAiNaturalLanguageQueryInterpreter.ParseReasoningEffort(effort));
    }

    [Fact]
    public void ParseReasoningEffort_WhenUnsupported_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(
            () => OpenAiNaturalLanguageQueryInterpreter.ParseReasoningEffort("minimal"));
    }

    private static OpenAiNaturalLanguageQueryInterpreter CreateRackInterpreter()
    {
        SemanticMappingConfiguration configuration = new()
        {
            EntityMappings =
            [
                new EntityMappingRule
                {
                    EntityType = SemanticEntityType.Rack,
                    Properties =
                    [
                        new PropertyMappingRule
                        {
                            SourceAttribute = "CAPACITY",
                            TargetProperty = "capacity",
                            DataType = SemanticDataType.Integer
                        }
                    ]
                }
            ]
        };

        return new OpenAiNaturalLanguageQueryInterpreter(
            null,
            null,
            configuration);
    }

    private static CadEntity CreateEntity(
        string id,
        SemanticEntityType entityType)
    {
        return new CadEntity
        {
            Id = id,
            Type = entityType,
            Name = id,
            RawName = id,
            Layer = "TEST",
            Position = new CadPoint(0, 0, 0),
            RotationRadians = 0
        };
    }
}
