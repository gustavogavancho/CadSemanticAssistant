using CadSemanticAssistant.Domain;

namespace CadSemanticAssistant.Application.Semantics;

public interface IBlockSemanticMapper
{
    public SemanticMappingResult Map(CadBlock block);
}
