using CadSemanticAssistant.Application.Queries.Validation;
using CadSemanticAssistant.Application.Semantics;
using CadSemanticAssistant.Application.Semantics.Configuration;
using CadSemanticAssistant.Domain.Queries;
using CadSemanticAssistant.Domain.Semantics;

namespace CadSemanticAssistant.Tests.Queries.Validation;

public sealed class SemanticQueryValidatorTests
{
    private readonly SemanticQueryValidator _validator;

    public SemanticQueryValidatorTests()
    {
        _validator = new SemanticQueryValidator(CreateConfiguration());
    }

    [Fact]
    public void Validate_ValidWithinDistance_ShouldBeValid()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            SpatialFilter = new SpatialFilter
            {
                Relation = SpatialRelation.WithinDistance,
                ReferenceEntityIds = ["M1"],
                MaxDistance = 10000
            }
        };
        Assert.True(_validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_WithinDistanceWithoutReference_ShouldFail()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            SpatialFilter = new SpatialFilter
            {
                Relation = SpatialRelation.WithinDistance,
                MaxDistance = 10000
            }
        };
        Assert.Contains(_validator.Validate(query).Errors,
            error => error.Code == SemanticQueryValidationErrorCode.InvalidSpatialFilter);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_WithinDistanceInvalidDistance_ShouldFail(double distance)
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            SpatialFilter = new SpatialFilter
            {
                Relation = SpatialRelation.WithinDistance,
                ReferenceEntityIds = ["M1"],
                MaxDistance = distance
            }
        };
        Assert.Contains(_validator.Validate(query).Errors,
            error => error.Code == SemanticQueryValidationErrorCode.InvalidSpatialFilter);
    }

    [Fact]
    public void Validate_NearestWithMaxDistance_ShouldFail()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            SpatialFilter = new SpatialFilter
            {
                Relation = SpatialRelation.Nearest,
                ReferenceEntityIds = ["M1"],
                MaxDistance = 5000
            }
        };
        Assert.False(_validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_ValidRackQuery_ShouldBeValid()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            Filters =
            [
                new QueryFilter
                {
                    Property = "model",
                    Operator = QueryOperator.Equals,
                    Value = "SC2"
                },
                new QueryFilter
                {
                    Property = "height",
                    Operator = QueryOperator.GreaterThan,
                    Value = 10000d
                }
            ]
        };

        SemanticQueryValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_QueryWithoutFilters_ShouldBeValid()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack
        };

        SemanticQueryValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_QueryWithValidScope_ShouldBeValid()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            ScopeEntityIds = ["1", "2"]
        };

        SemanticQueryValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_QueryWithBlankScopeEntityId_ShouldFail()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            ScopeEntityIds = ["1", " "]
        };

        SemanticQueryValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        AssertContainsError(
            result,
            SemanticQueryValidationErrorCode.InvalidScope);
    }

    [Fact]
    public void Validate_UnknownProperty_ShouldFail()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            Filters =
            [
                new QueryFilter
                {
                    Property = "weight",
                    Operator = QueryOperator.GreaterThan,
                    Value = 100d
                }
            ]
        };

        SemanticQueryValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        AssertContainsError(
            result,
            SemanticQueryValidationErrorCode.InvalidProperty);
    }

    [Fact]
    public void Validate_ContainsOnNumericProperty_ShouldFail()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            Filters =
            [
                new QueryFilter
                {
                    Property = "height",
                    Operator = QueryOperator.Contains,
                    Value = 10000d
                }
            ]
        };

        SemanticQueryValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        AssertContainsError(
            result,
            SemanticQueryValidationErrorCode.InvalidOperator);
    }

    [Fact]
    public void Validate_GreaterThanOnString_ShouldFail()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            Filters =
            [
                new QueryFilter
                {
                    Property = "model",
                    Operator = QueryOperator.GreaterThan,
                    Value = "SC2"
                }
            ]
        };

        SemanticQueryValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        AssertContainsError(
            result,
            SemanticQueryValidationErrorCode.InvalidOperator);
    }

    [Fact]
    public void Validate_StringValueForNumericProperty_ShouldFail()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            Filters =
            [
                new QueryFilter
                {
                    Property = "height",
                    Operator = QueryOperator.GreaterThan,
                    Value = "10000"
                }
            ]
        };

        SemanticQueryValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        AssertContainsError(
            result,
            SemanticQueryValidationErrorCode.InvalidValue);
    }

    [Fact]
    public void Validate_MachineCapacity_ShouldFail()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Machine,
            Filters =
            [
                new QueryFilter
                {
                    Property = "capacity",
                    Operator = QueryOperator.GreaterThan,
                    Value = 20
                }
            ]
        };

        SemanticQueryValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Equal(
            SemanticQueryValidationErrorCode.InvalidProperty,
            Assert.Single(result.Errors).Code);
    }

    private static void AssertContainsError(
        SemanticQueryValidationResult result,
        SemanticQueryValidationErrorCode errorCode)
    {
        Assert.Contains(
            result.Errors,
            error => error.Code == errorCode);
    }

    [Fact]
    public void Validate_PreviousResultsSourceWithoutIds_ShouldFail()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            ScopeSource = QueryScopeSource.PreviousResults,
            ScopeEntityIds = []
        };
        SemanticQueryValidationResult result = _validator.Validate(query);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Code == SemanticQueryValidationErrorCode.InvalidScope);
    }

    private static SemanticMappingConfiguration CreateConfiguration()
    {
        return new SemanticMappingConfiguration
        {
            EntityMappings =
            [
                new EntityMappingRule
                {
                    EntityType = SemanticEntityType.Rack,
                    BlockNamePatterns = ["RACK_*"],
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
                            SourceAttribute = "HEIGHT",
                            TargetProperty = "height",
                            DataType = SemanticDataType.Double,
                            Unit = "mm"
                        },
                        new PropertyMappingRule
                        {
                            SourceAttribute = "CAPACITY",
                            TargetProperty = "capacity",
                            DataType = SemanticDataType.Integer
                        }
                    ]
                },
                new EntityMappingRule
                {
                    EntityType = SemanticEntityType.Machine,
                    BlockNamePatterns = ["MACHINE_*"],
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
                }
            ]
        };
    }
}
