using CadSemanticAssistant.Domain.Catalog;

namespace CadSemanticAssistant.Application.Catalog;

public interface IBlockCatalogProvider
{
    public BlockCatalog GetCatalog();
}
