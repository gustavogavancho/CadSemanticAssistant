using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using CadSemanticAssistant.Application.Catalog;
using CadSemanticAssistant.Domain.Catalog;
using CadSemanticAssistant.Infrastructure.Configuration.Catalog;
using CadSemanticAssistant.Plugin.Infrastructure;

namespace CadSemanticAssistant.Plugin.Commands;

/// <summary>
/// Read-only diagnostic command. Does not change drawings or the query pipeline.
/// </summary>
public static class BlockCatalogCommands
{
    [CommandMethod("CSA_CATALOG_INFO")]
    public static void ShowCatalogInfo()
    {
        Document? document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            return;
        }

        try
        {
            IBlockCatalogProvider provider = new JsonBlockCatalogProvider(PluginPaths.BlockCatalogConfiguration);
            BlockCatalog catalog = provider.GetCatalog();
            document.Editor.WriteMessage($"\nCSA catalog v{catalog.SchemaVersion}: {catalog.CatalogItems.Length} models.");
            foreach (CatalogBlockDefinition item in catalog.CatalogItems)
            {
                document.Editor.WriteMessage($"\n - {item.BlockName}: {item.SemanticType}");
            }
        }
        catch (System.Exception exception)
        {
            document.Editor.WriteMessage($"\nCSA catalog error: {exception.Message}");
        }
    }
}
