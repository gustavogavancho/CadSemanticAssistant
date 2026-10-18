using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CadSemanticAssistant.Application.Selection;
using CadSemanticAssistant.Domain.Models;

namespace CadSemanticAssistant.Infrastructure.AutoCAD.Selection;

public sealed class AutoCadEntitySelectionService : ICadEntitySelectionService
{
    private readonly Database _database;
    private readonly Editor _editor;

    public AutoCadEntitySelectionService(Database database, Editor editor)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));

        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
    }

    public CadSelectionResult Select(IReadOnlyCollection<CadEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        List<ObjectId> objectIds = [];
        List<string> unresolvedEntityIds = [];

        foreach (CadEntity entity in entities)
        {
            bool resolved = TryResolveObjectId(entity.Id, out ObjectId objectId);

            if (!resolved)
            {
                unresolvedEntityIds.Add(entity.Id);

                continue;
            }

            objectIds.Add(objectId);
        }

        ObjectId[] selectedObjectIds = objectIds.Distinct().ToArray();

        _editor.SetImpliedSelection(selectedObjectIds);

        return new CadSelectionResult
        {
            RequestedCount = entities.Count,

            SelectedCount = selectedObjectIds.Length,

            UnresolvedEntityIds = unresolvedEntityIds
        };
    }

    public void ClearSelection()
    {
        _editor.SetImpliedSelection(Array.Empty<ObjectId>());
    }

    private bool TryResolveObjectId(string entityId, out ObjectId objectId)
    {
        objectId = ObjectId.Null;

        if (string.IsNullOrWhiteSpace(entityId))
        {
            return false;
        }

        bool parsed = long.TryParse(entityId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long handleValue);

        if (!parsed)
        {
            return false;
        }

        Handle handle = new(handleValue);

        try
        {
            ObjectId resolvedObjectId = _database.GetObjectId(false, handle, 0);

            if (resolvedObjectId.IsNull)
            {
                return false;
            }

            if (resolvedObjectId.IsErased)
            {
                return false;
            }

            objectId = resolvedObjectId;

            return true;
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            return false;
        }
    }
}
