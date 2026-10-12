using CadSemanticAssistant.Domain.Models;

namespace CadSemanticAssistant.Application.Semantics;

public interface IBlockSemanticMapper
{
    public SemanticMappingResult Map(CadBlock block);
}
