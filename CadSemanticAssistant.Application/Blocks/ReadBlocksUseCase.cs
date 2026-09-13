using CadSemanticAssistant.Domain;

namespace CadSemanticAssistant.Application.Blocks;

public sealed class ReadBlocksUseCase
{
    private readonly ICadBlockReader _blockReader;

    public ReadBlocksUseCase(ICadBlockReader blockReader)
    {
        _blockReader = blockReader;
    }

    public IReadOnlyCollection<CadBlock> Execute()
    {
        return _blockReader.ReadModelSpaceBlocks();
    }
}
