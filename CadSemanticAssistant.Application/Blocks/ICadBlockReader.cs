using CadSemanticAssistant.Domain.Models;

namespace CadSemanticAssistant.Application.Blocks;

public interface ICadBlockReader
{
    public IReadOnlyCollection<CadBlock> ReadModelSpaceBlocks();
}
