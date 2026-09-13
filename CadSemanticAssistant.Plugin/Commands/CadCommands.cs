using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using CadSemanticAssistant.Application.Blocks;
using CadSemanticAssistant.Domain;
using CadSemanticAssistant.Infrastructure.AutoCAD.Blocks;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

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
}
