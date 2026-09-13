using Autodesk.AutoCAD.DatabaseServices;

using CadSemanticAssistant.Application.Blocks;
using CadSemanticAssistant.Domain;

namespace CadSemanticAssistant.Infrastructure.AutoCAD.Blocks;

public sealed class AutoCadBlockReader : ICadBlockReader
{
    private readonly Database _database;

    public AutoCadBlockReader(Database database)
    {
        _database = database;
    }

    public IReadOnlyCollection<CadBlock> ReadModelSpaceBlocks()
    {
        List<CadBlock> blocks = [];

        using Transaction transaction = _database.TransactionManager.StartTransaction();

        BlockTable blockTable = (BlockTable)transaction.GetObject(_database.BlockTableId, OpenMode.ForRead);

        BlockTableRecord modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        foreach (ObjectId objectId in modelSpace)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not BlockReference blockReference)
            {
                continue;
            }

            CadBlock block = MapBlock(blockReference, transaction);

            blocks.Add(block);
        }

        transaction.Commit();

        return blocks;
    }

    private static CadBlock MapBlock(BlockReference blockReference, Transaction transaction)
    {
        BlockTableRecord blockDefinition = (BlockTableRecord)transaction.GetObject(blockReference.BlockTableRecord, OpenMode.ForRead);

        string rawName = blockDefinition.Name;

        string effectiveName = GetEffectiveBlockName(blockReference, blockDefinition, transaction);

        Dictionary<string, string> attributes = ReadAttributes(blockReference, transaction);

        bool isExternalReference = blockDefinition.IsFromExternalReference || blockDefinition.IsFromOverlayReference;

        CadPoint position = new(blockReference.Position.X, blockReference.Position.Y, blockReference.Position.Z);

        return new CadBlock
        {
            Id = blockReference.Handle.ToString(),

            Name = effectiveName,

            RawName = rawName,

            Layer = blockReference.Layer,

            Position = position,

            RotationRadians = blockReference.Rotation,

            IsDynamic = blockReference.IsDynamicBlock,

            IsExternalReference = isExternalReference,

            Attributes = attributes
        };
    }

    private static string GetEffectiveBlockName(BlockReference blockReference, BlockTableRecord blockDefinition, Transaction transaction)
    {
        if (!blockReference.IsDynamicBlock)
        {
            return blockDefinition.Name;
        }

        BlockTableRecord dynamicBlockDefinition = (BlockTableRecord)transaction.GetObject(blockReference.DynamicBlockTableRecord, OpenMode.ForRead);

        return dynamicBlockDefinition.Name;
    }

    private static Dictionary<string, string> ReadAttributes(BlockReference blockReference, Transaction transaction)
    {
        Dictionary<string, string> attributes = new(StringComparer.OrdinalIgnoreCase);

        foreach (ObjectId attributeId in blockReference.AttributeCollection)
        {
            if (transaction.GetObject(attributeId, OpenMode.ForRead) is not AttributeReference attribute)
            {
                continue;
            }

            attributes[attribute.Tag] = attribute.TextString;
        }

        return attributes;
    }
}
