using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CadSemanticAssistant.Application.Interaction;
using CadSemanticAssistant.Domain.Models;

namespace CadSemanticAssistant.Plugin.Services;

public sealed class AutoCadInteractionContextProvider
    : IInteractionContextProvider
{
    private readonly AutoCadSemanticEntityReader
        _semanticEntityReader;

    public AutoCadInteractionContextProvider(
        AutoCadSemanticEntityReader semanticEntityReader)
    {
        _semanticEntityReader =
            semanticEntityReader
            ?? throw new ArgumentNullException(
                nameof(semanticEntityReader));
    }

    public InteractionContext GetCurrentContext()
    {
        Document document =
            GetActiveDocument();

        using DocumentLock documentLock =
            document.LockDocument();

        PromptSelectionResult selectionResult =
            document.Editor.SelectImplied();

        if (selectionResult.Status !=
                PromptStatus.OK ||
            selectionResult.Value is null ||
            selectionResult.Value.Count == 0)
        {
            return new InteractionContext();
        }

        HashSet<string> selectedHandles =
            GetSelectedHandles(
                document.Database,
                selectionResult.Value);

        if (selectedHandles.Count == 0)
        {
            return new InteractionContext();
        }

        IReadOnlyCollection<CadEntity> entities =
            _semanticEntityReader.Read(
                document.Database);

        CadEntity[] selectedEntities =
            entities
                .Where(
                    entity =>
                        selectedHandles.Contains(
                            entity.Id))
                .ToArray();

        return new InteractionContext
        {
            SelectedEntities =
                selectedEntities
        };
    }

    private static HashSet<string> GetSelectedHandles(
        Database database,
        SelectionSet selectionSet)
    {
        HashSet<string> handles =
            new(
                StringComparer.OrdinalIgnoreCase);

        using Transaction transaction =
            database.TransactionManager.StartTransaction();

        foreach (
            ObjectId objectId
            in selectionSet.GetObjectIds())
        {
            if (objectId.IsNull ||
                objectId.IsErased ||
                !objectId.IsValid)
            {
                continue;
            }

            DBObject? databaseObject =
                transaction.GetObject(
                    objectId,
                    OpenMode.ForRead,
                    false);

            if (databaseObject is null)
            {
                continue;
            }

            handles.Add(
                databaseObject.Handle.ToString());
        }

        transaction.Commit();

        return handles;
    }

    private static Document GetActiveDocument()
    {
        return Autodesk.AutoCAD.ApplicationServices.Application
                   .DocumentManager
                   .MdiActiveDocument
               ?? throw new InvalidOperationException(
                   "There is no active AutoCAD drawing.");
    }
}
