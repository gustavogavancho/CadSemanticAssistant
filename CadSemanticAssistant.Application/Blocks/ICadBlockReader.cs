using CadSemanticAssistant.Domain;

namespace CadSemanticAssistant.Application.Blocks;

public interface ICadBlockReader
{
    public IReadOnlyCollection<CadBlock> ReadModelSpaceBlocks();
}
