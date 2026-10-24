using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CadSemanticAssistant.Application.Blocks;
using CadSemanticAssistant.Application.Semantics;
using CadSemanticAssistant.Application.Semantics.Configuration;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Semantics;
using CadSemanticAssistant.Infrastructure.AutoCAD.Blocks;
using CadSemanticAssistant.Infrastructure.Configuration.Semantics;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using CadSemanticAssistant.Application.Queries;
using CadSemanticAssistant.Domain.Queries;
using CadSemanticAssistant.Application.Selection;
using CadSemanticAssistant.Infrastructure.AutoCAD.Selection;
using CadSemanticAssistant.Plugin.Infrastructure;
using CadSemanticAssistant.Plugin.UI;

namespace CadSemanticAssistant.Plugin.Commands;

public sealed class CadCommands
{
    [CommandMethod("CSA_PING")]
    public void Ping()
    {
        Document? document = AcadApplication.DocumentManager.MdiActiveDocument;

        document?.Editor.WriteMessage("\nCAD Semantic Assistant is running.");
    }

    [CommandMethod("CSA_READBLOCKS")]
    public void ReadBlocks()
    {
        Document? document = AcadApplication.DocumentManager.MdiActiveDocument;

        if (document is null)
        {
            return;
        }

        Editor editor = document.Editor;
        Database database = document.Database;

        try
        {
            ICadBlockReader blockReader = new AutoCadBlockReader(database);

            ReadBlocksUseCase useCase = new(blockReader);

            IReadOnlyCollection<CadBlock> blocks = useCase.Execute();

            PrintBlocks(editor, blocks);
        }
        catch (System.Exception exception)
        {
            editor.WriteMessage($"\nError reading blocks: {exception.Message}");
        }
    }

    private static void PrintBlocks(Editor editor, IReadOnlyCollection<CadBlock> blocks)
    {
        editor.WriteMessage($"\n\nFound {blocks.Count} block references.");

        int index = 1;

        foreach (CadBlock block in blocks)
        {
            double rotationDegrees = block.RotationRadians * 180.0 / Math.PI;

            editor.WriteMessage($"\n\n[{index}]");

            editor.WriteMessage($"\nId: {block.Id}");

            editor.WriteMessage($"\nName: {block.Name}");

            if (!string.Equals(block.Name, block.RawName, StringComparison.Ordinal))
            {
                editor.WriteMessage($"\nRaw name: {block.RawName}");
            }

            editor.WriteMessage($"\nLayer: {block.Layer}");

            editor.WriteMessage(
                $"\nPosition: " +
                $"X={block.Position.X:0.###}, " +
                $"Y={block.Position.Y:0.###}, " +
                $"Z={block.Position.Z:0.###}");

            editor.WriteMessage($"\nRotation: {rotationDegrees:0.###}°");

            editor.WriteMessage($"\nDynamic: {block.IsDynamic}");

            editor.WriteMessage($"\nXRef: {block.IsExternalReference}");

            if (block.Attributes.Count > 0)
            {
                editor.WriteMessage("\nAttributes:");

                foreach (KeyValuePair<string, string> attribute in block.Attributes)
                {
                    editor.WriteMessage($"\n  {attribute.Key} = {attribute.Value}");
                }
            }

            index++;
        }

        editor.WriteMessage("\n");
    }

    [CommandMethod("CSA_READENTITIES")]
    public void ReadSemanticEntities()
    {
        Document? document = AcadApplication.DocumentManager.MdiActiveDocument;

        if (document is null)
        {
            return;
        }

        Editor editor = document.Editor;

        Database database = document.Database;

        try
        {
            ICadBlockReader blockReader = new AutoCadBlockReader(database);

            string configurationPath = PluginPaths.SemanticMappingConfiguration;

            ISemanticMappingConfigurationProvider configurationProvider = new JsonSemanticMappingConfigurationProvider(configurationPath);

            SemanticMappingConfiguration configuration = configurationProvider.GetConfiguration();

            IBlockSemanticMapper semanticMapper = new RuleBasedCadSemanticMapper(configuration);

            ReadSemanticEntitiesUseCase useCase = new(blockReader, semanticMapper);

            IReadOnlyCollection<SemanticMappingResult> results = useCase.Execute();

            PrintSemanticEntities(editor, results);
        }
        catch (System.Exception exception)
        {
            editor.WriteMessage($"\nError reading semantic entities: {exception.Message}");
        }
    }

    private static void PrintSemanticEntities(Editor editor, IReadOnlyCollection<SemanticMappingResult> results)
    {
        editor.WriteMessage($"\n\nFound {results.Count} semantic entities.");

        int index = 1;

        foreach (SemanticMappingResult result in results)
        {
            CadEntity entity = result.Entity;

            editor.WriteMessage($"\n\n[{index}]");

            editor.WriteMessage($"\nId: {entity.Id}");

            editor.WriteMessage($"\nType: {entity.Type}");

            editor.WriteMessage($"\nName: {entity.Name}");

            editor.WriteMessage($"\nLayer: {entity.Layer}");

            editor.WriteMessage(
                $"\nPosition: " +
                $"X={entity.Position.X:0.###}, " +
                $"Y={entity.Position.Y:0.###}, " +
                $"Z={entity.Position.Z:0.###}");

            if (entity.Properties.Count > 0)
            {
                editor.WriteMessage("\nSemantic properties:");

                foreach (KeyValuePair<string, SemanticPropertyValue> property in entity.Properties)
                {
                    string unit = string.IsNullOrWhiteSpace(property.Value.Unit) ? string.Empty : $" {property.Value.Unit}";

                    editor.WriteMessage($"\n  {property.Key} = " + $"{property.Value.Value}{unit}");
                }
            }

            if (result.Warnings.Count > 0)
            {
                editor.WriteMessage("\nWarnings:");

                foreach (string warning in result.Warnings)
                {
                    editor.WriteMessage($"\n  - {warning}");
                }
            }

            index++;
        }

        editor.WriteMessage("\n");
    }

    [CommandMethod("CSA_TESTQUERY")]
    public void TestSemanticQuery()
    {
        Document? document = AcadApplication.DocumentManager.MdiActiveDocument;

        if (document is null)
        {
            return;
        }

        Editor editor = document.Editor;

        Database database = document.Database;

        try
        {
            IReadOnlyCollection<CadEntity> entities = ReadSemanticEntities(database);

            SemanticQuery query = CreateTestQuery();

            ISemanticQueryEngine queryEngine = new SemanticQueryEngine();

            SemanticQueryResult queryResult = queryEngine.Execute(query, entities);

            PrintQueryResult(editor, queryResult);

            ICadEntitySelectionService selectionService = new AutoCadEntitySelectionService(database, editor);

            CadSelectionResult selectionResult = selectionService.Select(queryResult.Entities);

            PrintSelectionResult(editor, selectionResult);
        }
        catch (System.Exception exception)
        {
            editor.WriteMessage($"\nError executing semantic query: {exception.Message}");
        }
    }

    private static SemanticQuery CreateTestQuery()
    {
        return new SemanticQuery
        {
            Intent =
                QueryIntent.Select,

            EntityType =
                SemanticEntityType.Machine,

            Filters =
            [
                new QueryFilter
            {
                Property =
                    "machineType",

                Operator =
                    QueryOperator.Equals,

                Value =
                    "Miniload"
            }
            ]
        };
    }

    private static void PrintSelectionResult(Editor editor, CadSelectionResult result)
    {
        editor.WriteMessage("\n\nAutoCAD selection:");

        editor.WriteMessage($"\nRequested: {result.RequestedCount}");

        editor.WriteMessage($"\nSelected: {result.SelectedCount}");

        if (result.UnresolvedEntityIds.Count > 0)
        {
            editor.WriteMessage("\nUnresolved entity IDs:");

            foreach (string entityId in result.UnresolvedEntityIds)
            {
                editor.WriteMessage($"\n  - {entityId}");
            }
        }

        editor.WriteMessage("\n");
    }

    private static IReadOnlyCollection<CadEntity> ReadSemanticEntities(Database database)
    {
        ICadBlockReader blockReader = new AutoCadBlockReader(database);

        string configurationPath = PluginPaths.SemanticMappingConfiguration;

        ISemanticMappingConfigurationProvider configurationProvider = new JsonSemanticMappingConfigurationProvider(configurationPath);

        SemanticMappingConfiguration configuration = configurationProvider.GetConfiguration();

        IBlockSemanticMapper semanticMapper = new RuleBasedCadSemanticMapper(configuration);

        ReadSemanticEntitiesUseCase useCase = new(blockReader, semanticMapper);

        IReadOnlyCollection<SemanticMappingResult> mappingResults = useCase.Execute();

        return mappingResults.Select(result => result.Entity).ToArray();
    }

    private static void PrintQueryResult(Editor editor, SemanticQueryResult result)
    {
        editor.WriteMessage("\n\nSemantic query executed.");

        editor.WriteMessage($"\nIntent: {result.Query.Intent}");

        editor.WriteMessage($"\nEntity type: {result.Query.EntityType}");

        editor.WriteMessage($"\nFilters: {result.Query.Filters.Count}");

        editor.WriteMessage($"\nResults: {result.Count}");

        int index = 1;

        foreach (CadEntity entity in result.Entities)
        {
            editor.WriteMessage($"\n\n[{index}]");

            editor.WriteMessage($"\nId: {entity.Id}");

            editor.WriteMessage($"\nName: {entity.Name}");

            editor.WriteMessage($"\nType: {entity.Type}");

            foreach (KeyValuePair<string, SemanticPropertyValue> property in entity.Properties)
            {
                string unit = string.IsNullOrWhiteSpace(property.Value.Unit) ? string.Empty : $" {property.Value.Unit}";

                editor.WriteMessage($"\n{property.Key}: " + $"{property.Value.Value}{unit}");
            }

            index++;
        }

        editor.WriteMessage("\n");
    }

    [CommandMethod("CSA_CLEARSELECTION")]
    public void ClearSelection()
    {
        Document? document = AcadApplication.DocumentManager.MdiActiveDocument;

        if (document is null)
        {
            return;
        }

        ICadEntitySelectionService selectionService = new AutoCadEntitySelectionService(document.Database, document.Editor);

        selectionService.ClearSelection();

        document.Editor.WriteMessage("\nSelection cleared.");
    }

    [CommandMethod("CSA_SHOWPALETTE")]
    public void ShowPalette()
    {
        try
        {
            SemanticAssistantPaletteHost.Show();
        }
        catch (System.Exception exception)
        {
            Document? document = AcadApplication.DocumentManager.MdiActiveDocument;
            document?.Editor.WriteMessage(
                $"\nError showing Semantic Assistant palette: {exception}");
        }
    }
}
