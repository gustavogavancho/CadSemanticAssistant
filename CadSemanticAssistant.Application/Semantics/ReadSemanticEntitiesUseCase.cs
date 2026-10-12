using CadSemanticAssistant.Application.Blocks;
using CadSemanticAssistant.Domain.Models;

namespace CadSemanticAssistant.Application.Semantics;

public sealed class ReadSemanticEntitiesUseCase
{
    private readonly ICadBlockReader _blockReader;

    private readonly IBlockSemanticMapper _semanticMapper;

    public ReadSemanticEntitiesUseCase(ICadBlockReader blockReader, IBlockSemanticMapper semanticMapper)
    {
        _blockReader = blockReader;
        _semanticMapper = semanticMapper;
    }

    public IReadOnlyCollection<SemanticMappingResult> Execute()
    {
        IReadOnlyCollection<CadBlock> blocks =
            _blockReader.ReadModelSpaceBlocks();

        List<SemanticMappingResult> results = [];

        foreach (CadBlock block in blocks)
        {
            SemanticMappingResult result = _semanticMapper.Map(block);

            results.Add(result);
        }

        return results;
    }
}
