using CadSemanticAssistant.Application.Queries;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Queries;
using CadSemanticAssistant.Domain.Semantics;

namespace CadSemanticAssistant.Tests.Queries;

public sealed class SemanticQueryEngineTests
{
    private readonly SemanticQueryEngine _sut;
    private readonly IReadOnlyCollection<CadEntity> _entities;

    public SemanticQueryEngineTests()
    {
        _sut = new SemanticQueryEngine();
        _entities = CreateEntities();
    }

    [Theory]
    [InlineData("SC2", 10000d, 2)]
    [InlineData("SC2", 12000d, 1)]
    [InlineData("SC2", 15000d, 0)]
    [InlineData("SC1", 10000d, 1)]
    [InlineData("SC3", 10000d, 1)]
    public void Execute_RacksFilteredByModelAndHeight_ShouldReturnExpectedCount(string model, double minimumHeight, int expectedCount)
    {
        // Arrange
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
                    Value = model
                },
                new QueryFilter
                {
                    Property = "height",
                    Operator = QueryOperator.GreaterThan,
                    Value = minimumHeight
                }
            ]
        };

        // Act
        SemanticQueryResult result = _sut.Execute(query, _entities);

        // Assert
        Assert.Equal(expectedCount, result.Count);
    }

    [Fact]
    public void Execute_RacksFilteredByModel_ShouldReturnExpectedCount()
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
                }
            ]
        };

        SemanticQueryResult result = _sut.Execute(query, _entities);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Execute_RacksFilteredByHeight_ShouldReturnExpectedCount()
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
                    Value = 10000d
                }
            ]
        };

        SemanticQueryResult result = _sut.Execute(query, _entities);

        Assert.Equal(4, result.Count);
    }

    [Fact]
    public void Execute_ScopedRackQuery_ShouldReturnOnlyMatchingScopedEntities()
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
                    Value = 10000d
                }
            ],
            ScopeEntityIds = ["1", "2", "5"]
        };

        SemanticQueryResult result = _sut.Execute(query, _entities);

        Assert.Equal(2, result.Count);
        Assert.Equal(
            new[] { "1", "5" },
            result.Entities.Select(entity => entity.Id).OrderBy(id => id));
    }

    [Fact]
    public void Execute_RacksFilteredByCapacity_ShouldReturnExpectedCount()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            Filters =
            [
                new QueryFilter
                {
                    Property = "capacity",
                    Operator = QueryOperator.GreaterThanOrEqual,
                    Value = 40
                }
            ]
        };

        SemanticQueryResult result = _sut.Execute(query, _entities);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Execute_RacksFilteredByUnknownModel_ShouldReturnNoResults()
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
                    Value = "UNKNOWN"
                }
            ]
        };

        SemanticQueryResult result = _sut.Execute(query, _entities);

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void Execute_MachinesFilteredByMachineType_ShouldReturnExpectedCount()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Machine,
            Filters =
            [
                new QueryFilter
                {
                    Property = "machineType",
                    Operator = QueryOperator.Equals,
                    Value = "Miniload"
                }
            ]
        };

        SemanticQueryResult result = _sut.Execute(query, _entities);

        Assert.Equal(1, result.Count);
    }

    [Fact]
    public void Execute_WithinDistance_ReturnsOnlyNearbyRacks()
    {
        CadEntity machine = CreateSpatialEntity("M1", SemanticEntityType.Machine, 0, 0);
        CadEntity near1 = CreateSpatialEntity("R1", SemanticEntityType.Rack, 3000, 0);
        CadEntity near2 = CreateSpatialEntity("R2", SemanticEntityType.Rack, 7000, 0);
        CadEntity far = CreateSpatialEntity("R3", SemanticEntityType.Rack, 12000, 0);
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            SpatialFilter = new SpatialFilter
            {
                Relation = SpatialRelation.WithinDistance,
                ReferenceEntityIds = ["M1"],
                MaxDistance = 8000
            }
        };
        SemanticQueryResult result = _sut.Execute(query, [machine, near1, near2, far]);
        Assert.Equal(new[] { "R1", "R2" },
            result.Entities.Select(entity => entity.Id).OrderBy(id => id));
    }

    [Fact]
    public void Execute_Nearest_ReturnsOneRack()
    {
        CadEntity machine = CreateSpatialEntity("M1", SemanticEntityType.Machine, 0, 0);
        CadEntity rack1 = CreateSpatialEntity("R1", SemanticEntityType.Rack, 9000, 0);
        CadEntity rack2 = CreateSpatialEntity("R2", SemanticEntityType.Rack, 4000, 0);
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            SpatialFilter = new SpatialFilter
            {
                Relation = SpatialRelation.Nearest,
                ReferenceEntityIds = ["M1"]
            }
        };
        SemanticQueryResult result = _sut.Execute(query, [machine, rack1, rack2]);
        Assert.Equal("R2", Assert.Single(result.Entities).Id);
    }

    [Fact]
    public void Execute_NearestBreaksTiesById()
    {
        CadEntity machine = CreateSpatialEntity("M1", SemanticEntityType.Machine, 0, 0);
        CadEntity rack2 = CreateSpatialEntity("R2", SemanticEntityType.Rack, -5000, 0);
        CadEntity rack1 = CreateSpatialEntity("R1", SemanticEntityType.Rack, 5000, 0);
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            SpatialFilter = new SpatialFilter
            {
                Relation = SpatialRelation.Nearest,
                ReferenceEntityIds = ["M1"]
            }
        };
        SemanticQueryResult result = _sut.Execute(query, [machine, rack2, rack1]);
        Assert.Equal("R1", Assert.Single(result.Entities).Id);
    }

    [Fact]
    public void Execute_NearestExcludesTheReferenceItself()
    {
        CadEntity rack1 = CreateSpatialEntity("R1", SemanticEntityType.Rack, 0, 0);
        CadEntity rack2 = CreateSpatialEntity("R2", SemanticEntityType.Rack, 2000, 0);
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            SpatialFilter = new SpatialFilter
            {
                Relation = SpatialRelation.Nearest,
                ReferenceEntityIds = ["R1"]
            }
        };
        SemanticQueryResult result = _sut.Execute(query, [rack1, rack2]);
        Assert.Equal("R2", Assert.Single(result.Entities).Id);
    }

    [Fact]
    public void Execute_SpatialFilterComposesWithScope()
    {
        CadEntity machine = CreateSpatialEntity("M1", SemanticEntityType.Machine, 0, 0);
        CadEntity rack1 = CreateSpatialEntity("R1", SemanticEntityType.Rack, 2000, 0);
        CadEntity rack2 = CreateSpatialEntity("R2", SemanticEntityType.Rack, 3000, 0);
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            ScopeEntityIds = ["R2"],
            SpatialFilter = new SpatialFilter
            {
                Relation = SpatialRelation.WithinDistance,
                ReferenceEntityIds = ["M1"],
                MaxDistance = 5000
            }
        };
        SemanticQueryResult result = _sut.Execute(query, [machine, rack1, rack2]);
        Assert.Equal("R2", Assert.Single(result.Entities).Id);
    }

    private static CadEntity CreateSpatialEntity(
        string id,
        SemanticEntityType type,
        double x,
        double y)
    {
        return new CadEntity
        {
            Id = id,
            Type = type,
            Name = id,
            RawName = id,
            Layer = "TEST",
            Position = new CadPoint(x, y, 0),
            RotationRadians = 0
        };
    }

    [Fact]
    public void Execute_ConversationScope_ShouldNeverExpandToEntireDrawing()
    {
        SemanticQuery query = new()
        {
            Intent = QueryIntent.Select,
            EntityType = SemanticEntityType.Rack,
            ScopeSource = QueryScopeSource.PreviousResults,
            ScopeEntityIds = ["1", "5"],
            Filters = [new QueryFilter
            {
                Property = "model", Operator = QueryOperator.Equals, Value = "SC2"
            }]
        };
        SemanticQueryResult result = _sut.Execute(query, _entities);
        Assert.Single(result.Entities);
        Assert.Equal("1", result.Entities.Single().Id);
    }

    private static IReadOnlyCollection<CadEntity> CreateEntities()
    {
        return
        [
            CreateRack("1", "SC2", 12000, 40),
            CreateRack("2", "SC2", 9000, 30),
            CreateRack("3", "SC2", 15000, 50),
            CreateRack("4", "SC1", 11000, 25),
            CreateRack("5", "SC3", 14000, 45),
            CreateMachine("6", "Miniload")
        ];
    }

    private static CadEntity CreateRack(string id, string model, double height, int capacity)
    {
        return new CadEntity
        {
            Id = id,
            Type = SemanticEntityType.Rack,
            Name = $"RACK_{model}",
            RawName = $"RACK_{model}",
            Layer = "RACKS",
            Position = new CadPoint(0, 0, 0),
            RotationRadians = 0,
            Properties = new Dictionary<string, SemanticPropertyValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["model"] = new()
                {
                    Value = model,
                    RawValue = model
                },
                ["height"] = new()
                {
                    Value = height,
                    RawValue = height.ToString(),
                    Unit = "mm"
                },
                ["capacity"] = new()
                {
                    Value = capacity,
                    RawValue = capacity.ToString()
                }
            }
        };
    }

    private static CadEntity CreateMachine(string id, string machineType)
    {
        return new CadEntity
        {
            Id = id,
            Type = SemanticEntityType.Machine,
            Name = "MACHINE_MINILOAD",
            RawName = "MACHINE_MINILOAD",
            Layer = "MACHINES",
            Position = new CadPoint(0, 0, 0),
            RotationRadians = 0,
            Properties = new Dictionary<string, SemanticPropertyValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["machineType"] = new()
                {
                    Value = machineType,
                    RawValue = machineType
                }
            }
        };
    }
}
