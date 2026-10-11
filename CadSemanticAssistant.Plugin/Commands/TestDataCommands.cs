using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CadSemanticAssistant.Infrastructure.AutoCAD.TestData;
using Exception = System.Exception;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadSemanticAssistant.Plugin.Commands;

public sealed class TestDataCommands
{
    [CommandMethod("CSA_SEEDTESTDATA")]
    public void SeedTestData()
    {
        Document? document = AcadApplication.DocumentManager.MdiActiveDocument;

        if (document is null)
        {
            return;
        }

        Editor editor =
            document.Editor;

        try
        {
            AutoCadTestDrawingSeeder seeder = new(document.Database);

            TestDrawingSeedResult result = seeder.Seed();

            editor.WriteMessage("\n\nCAD Semantic Assistant test data created.");

            editor.WriteMessage($"\nRacks: {result.RackCount}");

            editor.WriteMessage($"\nMachines: {result.MachineCount}");

            editor.WriteMessage("\n\nRun CSA_READBLOCKS to inspect raw blocks.");

            editor.WriteMessage("\nRun CSA_READENTITIES to inspect semantic entities.");

            editor.WriteMessage("\n");
        }
        catch (Exception exception)
        {
            editor.WriteMessage($"\nError creating test data: {exception.Message}");
        }
    }
}
