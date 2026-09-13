using System.Reflection;

using CadSemanticAssistant.Application.Blocks;
using CadSemanticAssistant.Domain;

namespace CadSemanticAssistant.Tests;

public sealed class BlocksTests
{
    [Fact]
    public void ReadBlocksUseCase_ReturnsBlocksFromReader()
    {
        IReadOnlyCollection<CadBlock> expected =
        [
            new CadBlock
            {
                Id = "1A",
                Name = "DOOR",
                RawName = "DOOR",
                Layer = "A-WALL",
                Position = new CadPoint(10, 20, 0),
                RotationRadians = Math.PI / 2,
                Attributes = new Dictionary<string, string>
                {
                    ["NUMBER"] = "101"
                }
            }
        ];

        ReadBlocksUseCase useCase = new(new StubCadBlockReader(expected));

        IReadOnlyCollection<CadBlock> actual = useCase.Execute();

        Assert.Same(expected, actual);
    }

    [Fact]
    public void CadBlock_ContainsAllAcceptanceData()
    {
        Dictionary<string, string> attributes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["TAG"] = "VALUE"
        };

        CadBlock block = new()
        {
            Id = "ABC",
            Name = "EFFECTIVE_NAME",
            RawName = "*U1",
            Layer = "A-WALL",
            Position = new CadPoint(1, 2, 3),
            RotationRadians = 0.75,
            IsDynamic = true,
            IsExternalReference = true,
            Attributes = attributes
        };

        Assert.Equal("ABC", block.Id);
        Assert.Equal("EFFECTIVE_NAME", block.Name);
        Assert.Equal("*U1", block.RawName);
        Assert.Equal("A-WALL", block.Layer);
        Assert.Equal(new CadPoint(1, 2, 3), block.Position);
        Assert.Equal(0.75, block.RotationRadians);
        Assert.True(block.IsDynamic);
        Assert.True(block.IsExternalReference);
        Assert.Equal("VALUE", block.Attributes["tag"]);
    }

    [Fact]
    public void ReadBlocksUseCase_ReturnsEmptyCollectionWhenDrawingHasNoBlocks()
    {
        IReadOnlyCollection<CadBlock> expected = [];
        ReadBlocksUseCase useCase = new(new StubCadBlockReader(expected));

        IReadOnlyCollection<CadBlock> actual = useCase.Execute();

        Assert.Empty(actual);
    }

    [Fact]
    public void CadPoint_UsesValueEqualityForCoordinates()
    {
        CadPoint first = new(1, 2, 3);
        CadPoint second = new(1, 2, 3);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Domain_DoesNotReferenceAutodesk()
    {
        Assembly domainAssembly = typeof(CadPoint).Assembly;

        Assert.DoesNotContain(
            domainAssembly.GetReferencedAssemblies(),
            reference => reference.Name is not null &&
                reference.Name.StartsWith("Autodesk.", StringComparison.Ordinal));
    }

    private sealed class StubCadBlockReader(IReadOnlyCollection<CadBlock> blocks) : ICadBlockReader
    {
        public IReadOnlyCollection<CadBlock> ReadModelSpaceBlocks()
        {
            return blocks;
        }
    }
}
