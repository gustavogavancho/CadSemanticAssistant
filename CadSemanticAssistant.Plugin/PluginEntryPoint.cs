using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using AcadApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadSemanticAssistant.Plugin;

public sealed class PluginEntryPoint : IExtensionApplication
{
    public void Initialize()
    {
        Document? document = AcadApplication.DocumentManager.MdiActiveDocument;

        if (document is null)
        {
            return;
        }

        Editor editor = document.Editor;

        editor.WriteMessage("\nCAD Semantic Assistant loaded.");

        editor.WriteMessage("\nCommand available: CSA_READBLOCKS");
    }

    public void Terminate()
    {
    }
}
