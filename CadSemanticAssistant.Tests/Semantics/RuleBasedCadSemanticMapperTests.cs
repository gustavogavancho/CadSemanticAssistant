using CadSemanticAssistant.Application.Semantics;
using CadSemanticAssistant.Application.Semantics.Configuration;
using CadSemanticAssistant.Domain;
using CadSemanticAssistant.Domain.Semantics;


namespace CadSemanticAssistant.Tests.Semantics;

public sealed class RuleBasedCadSemanticMapperTests
{
    [Fact]
    public void Map_RackBlock_ShouldCreateRackEntity()
    {
        SemanticMappingConfiguration configuration =
            CreateConfiguration();

        RuleBasedCadSemanticMapper mapper =
            new(configuration);

        CadBlock block = new()
        {
            Id = "ABC",

            Name = "RACK_SC2_A",

            RawName = "RACK_SC2_A",

            Layer = "RACKS",

            Position = new CadPoint(
                1000,
                2000,
                0),

            RotationRadians = 0,

            Attributes =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["MODEL"] = "SC2",
                    ["HEIGHT"] = "12000",
                    ["CAPACITY"] = "40"
                }
        };

        SemanticMappingResult result =
            mapper.Map(block);

        Assert.Equal(
            SemanticEntityType.Rack,
            result.Entity.Type);

        Assert.Equal(
            "SC2",
            result.Entity.Properties["model"].Value);

        Assert.Equal(
            12000d,
            result.Entity.Properties["height"].Value);

        Assert.Equal(
            40,
            result.Entity.Properties["capacity"].Value);

        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Map_UnknownBlock_ShouldCreateUnknownEntity()
    {
        SemanticMappingConfiguration configuration =
            CreateConfiguration();

        RuleBasedCadSemanticMapper mapper =
            new(configuration);

        CadBlock block = new()
        {
            Id = "XYZ",

            Name = "DESK_001",

            RawName = "DESK_001",

            Layer = "FURNITURE",

            Position = new CadPoint(
                0,
                0,
                0),

            RotationRadians = 0
        };

        SemanticMappingResult result =
            mapper.Map(block);

        Assert.Equal(
            SemanticEntityType.Unknown,
            result.Entity.Type);

        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Map_MachineBlock_ShouldCreateMachineEntity()
    {
        SemanticMappingConfiguration configuration = new()
        {
            EntityMappings =
            [
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
                        }
                    ]
                }
            ]
        };

        RuleBasedCadSemanticMapper mapper = new(configuration);

        CadBlock block = CreateBlock(
            "MACHINE_001",
            new Dictionary<string, string>
            {
                ["MODEL"] = "PRESS-01"
            });

        SemanticMappingResult result = mapper.Map(block);

        Assert.Equal(SemanticEntityType.Machine, result.Entity.Type);
        Assert.Equal("PRESS-01", result.Entity.Properties["model"].Value);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Map_NormalizesAttributeNamesAndValuesAndPreservesUnits()
    {
        RuleBasedCadSemanticMapper mapper = new(CreateConfiguration());

        CadBlock block = CreateBlock(
            "RACK_SC2_A",
            new Dictionary<string, string>
            {
                [" model "] = "  SC2  ",
                [" HEIGHT "] = " 12000 ",
                [" capacity "] = " 40 "
            });

        SemanticMappingResult result = mapper.Map(block);

        Assert.Equal("SC2", result.Entity.Properties["model"].Value);
        Assert.Equal(12000d, result.Entity.Properties["height"].Value);
        Assert.Equal(40, result.Entity.Properties["capacity"].Value);
        Assert.Equal("mm", result.Entity.Properties["height"].Unit);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Map_InvalidConversion_ShouldAddWarning()
    {
        RuleBasedCadSemanticMapper mapper = new(CreateConfiguration());

        CadBlock block = CreateBlock(
            "RACK_SC2_A",
            new Dictionary<string, string>
            {
                ["MODEL"] = "SC2",
                ["HEIGHT"] = "not-a-number"
            });

        SemanticMappingResult result = mapper.Map(block);

        Assert.Contains(
            result.Warnings,
            warning => warning.Contains(
                "HEIGHT",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("height", result.Entity.Properties.Keys);
    }

    [Fact]
    public void Map_DynamicBlock_ShouldUseEffectiveName()
    {
        RuleBasedCadSemanticMapper mapper = new(CreateConfiguration());

        CadBlock block = CreateBlock(
            "RACK_EFFECTIVE",
            new Dictionary<string, string>(),
            rawName: "*U123",
            isDynamic: true);

        SemanticMappingResult result = mapper.Map(block);

        Assert.Equal(SemanticEntityType.Rack, result.Entity.Type);
        Assert.Equal("RACK_EFFECTIVE", result.Entity.Name);
        Assert.Equal("*U123", result.Entity.RawName);
    }

    private static CadBlock CreateBlock(
        string name,
        IReadOnlyDictionary<string, string> attributes,
        string? rawName = null,
        bool isDynamic = false)
    {
        return new CadBlock
        {
            Id = "ABC",
            Name = name,
            RawName = rawName ?? name,
            Layer = "TEST",
            Position = new CadPoint(0, 0, 0),
            RotationRadians = 0,
            IsDynamic = isDynamic,
            Attributes = attributes
        };
    }

    private static SemanticMappingConfiguration CreateConfiguration()
    {
        return new SemanticMappingConfiguration
        {
            EntityMappings =
            [
                new EntityMappingRule
                {
                    EntityType =
                        SemanticEntityType.Rack,

                    BlockNamePatterns =
                    [
                        "RACK_*"
                    ],

                    Properties =
                    [
                        new PropertyMappingRule
                        {
                            SourceAttribute = "MODEL",
                            TargetProperty = "model",
                            DataType = SemanticDataType.String,
                            Required = true
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
                }
            ]
        };
    }
}
