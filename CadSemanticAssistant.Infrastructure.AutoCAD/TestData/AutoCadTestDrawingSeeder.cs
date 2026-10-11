using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadSemanticAssistant.Infrastructure.AutoCAD.TestData;

public sealed class AutoCadTestDrawingSeeder
{
    private const string RackLayer = "CSA_TEST_RACKS";
    private const string MachineLayer = "CSA_TEST_MACHINES";
    private readonly Database _database;

    public AutoCadTestDrawingSeeder(Database database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public TestDrawingSeedResult Seed()
    {
        using Transaction transaction = _database.TransactionManager.StartTransaction();

        EnsureLayer(transaction, RackLayer);

        EnsureLayer(transaction, MachineLayer);

        ClearExistingTestEntities(transaction);

        ObjectId rackSc1Definition = EnsureRackBlockDefinition(transaction,
                "RACK_SC1_A");

        ObjectId rackSc2Definition = EnsureRackBlockDefinition(transaction,
                "RACK_SC2_A");

        ObjectId rackSc3Definition = EnsureRackBlockDefinition(transaction,
                "RACK_SC3_A");

        ObjectId machineADefinition = EnsureMachineBlockDefinition(transaction,
                "MACHINE_A");

        ObjectId machineBDefinition = EnsureMachineBlockDefinition(transaction,
                "MACHINE_B");

        BlockTableRecord modelSpace = GetModelSpaceForWrite(transaction);

        int rackCount = 0;

        InsertBlock(
            transaction,
            modelSpace,
            rackSc2Definition,
            new Point3d(0, 0, 0),
            RackLayer,
            new Dictionary<string, string>
            {
                ["MODEL"] = "SC2",
                ["HEIGHT"] = "12000",
                ["CAPACITY"] = "40"
            });

        rackCount++;

        InsertBlock(
            transaction,
            modelSpace,
            rackSc2Definition,
            new Point3d(6000, 0, 0),
            RackLayer,
            new Dictionary<string, string>
            {
                ["MODEL"] = "SC2",
                ["HEIGHT"] = "9000",
                ["CAPACITY"] = "30"
            });

        rackCount++;

        InsertBlock(
            transaction,
            modelSpace,
            rackSc2Definition,
            new Point3d(12000, 0, 0),
            RackLayer,
            new Dictionary<string, string>
            {
                ["MODEL"] = "SC2",
                ["HEIGHT"] = "15000",
                ["CAPACITY"] = "50"
            });

        rackCount++;

        InsertBlock(
            transaction,
            modelSpace,
            rackSc1Definition,
            new Point3d(0, 5000, 0),
            RackLayer,
            new Dictionary<string, string>
            {
                ["MODEL"] = "SC1",
                ["HEIGHT"] = "8000",
                ["CAPACITY"] = "20"
            });

        rackCount++;

        InsertBlock(
            transaction,
            modelSpace,
            rackSc1Definition,
            new Point3d(6000, 5000, 0),
            RackLayer,
            new Dictionary<string, string>
            {
                ["MODEL"] = "SC1",
                ["HEIGHT"] = "11000",
                ["CAPACITY"] = "25"
            });

        rackCount++;

        InsertBlock(
            transaction,
            modelSpace,
            rackSc3Definition,
            new Point3d(12000, 5000, 0),
            RackLayer,
            new Dictionary<string, string>
            {
                ["MODEL"] = "SC3",
                ["HEIGHT"] = "14000",
                ["CAPACITY"] = "45"
            });

        rackCount++;

        int machineCount = 0;

        InsertBlock(
            transaction,
            modelSpace,
            machineADefinition,
            new Point3d(6000, 12000, 0),
            MachineLayer,
            new Dictionary<string, string>
            {
                ["MODEL"] = "MX100",
                ["TYPE"] = "Miniload"
            });

        machineCount++;

        InsertBlock(
            transaction,
            modelSpace,
            machineBDefinition,
            new Point3d(18000, 3000, 0),
            MachineLayer,
            new Dictionary<string, string>
            {
                ["MODEL"] = "MX200",
                ["TYPE"] = "StackerCrane"
            });

        machineCount++;

        transaction.Commit();

        return new TestDrawingSeedResult(rackCount, machineCount);
    }

    private void EnsureLayer(Transaction transaction, string layerName)
    {
        LayerTable layerTable = (LayerTable)transaction.GetObject(_database.LayerTableId, OpenMode.ForRead);

        if (layerTable.Has(layerName))
        {
            return;
        }

        layerTable.UpgradeOpen();

        LayerTableRecord layer = new() { Name = layerName };

        layerTable.Add(layer);

        transaction.AddNewlyCreatedDBObject(layer, true);
    }

    private ObjectId EnsureRackBlockDefinition(Transaction transaction, string blockName)
    {
        BlockTable blockTable = (BlockTable)transaction.GetObject(_database.BlockTableId, OpenMode.ForRead);

        if (blockTable.Has(blockName))
        {
            return blockTable[blockName];
        }

        blockTable.UpgradeOpen();

        BlockTableRecord definition = new() { Name = blockName, Origin = Point3d.Origin };

        ObjectId blockId = blockTable.Add(definition);

        transaction.AddNewlyCreatedDBObject(definition, true);

        CreateRackGeometry(transaction, definition);

        AddAttributeDefinition(transaction, definition, "MODEL", new Point3d(250, 750, 0));

        AddAttributeDefinition(transaction, definition, "HEIGHT", new Point3d(1500, 750, 0));

        AddAttributeDefinition(transaction, definition, "CAPACITY", new Point3d(2800, 750, 0));

        return blockId;
    }

    private ObjectId EnsureMachineBlockDefinition(Transaction transaction, string blockName)
    {
        BlockTable blockTable = (BlockTable)transaction.GetObject(_database.BlockTableId, OpenMode.ForRead);

        if (blockTable.Has(blockName))
        {
            return blockTable[blockName];
        }

        blockTable.UpgradeOpen();

        BlockTableRecord definition = new() { Name = blockName, Origin = Point3d.Origin };

        ObjectId blockId = blockTable.Add(definition);

        transaction.AddNewlyCreatedDBObject(definition, true);

        CreateMachineGeometry(transaction, definition);

        AddAttributeDefinition(transaction, definition, "MODEL", new Point3d(-700, 0, 0));

        AddAttributeDefinition(transaction, definition, "TYPE", new Point3d(-700, -400, 0));

        return blockId;
    }

    private static void CreateRackGeometry(Transaction transaction, BlockTableRecord definition)
    {
        Polyline polyline = new(4);

        polyline.AddVertexAt(0, new Point2d(0, 0), 0, 0, 0);

        polyline.AddVertexAt(1, new Point2d(4000, 0), 0, 0, 0);

        polyline.AddVertexAt(2, new Point2d(4000, 1000), 0, 0, 0);

        polyline.AddVertexAt(3, new Point2d(0, 1000), 0, 0, 0);

        polyline.Closed = true;

        definition.AppendEntity(polyline);

        transaction.AddNewlyCreatedDBObject(polyline, true);
    }

    private static void CreateMachineGeometry(Transaction transaction, BlockTableRecord definition)
    {
        Circle circle = new(Point3d.Origin, Vector3d.ZAxis, 1000);

        definition.AppendEntity(circle);

        transaction.AddNewlyCreatedDBObject(circle, true);
    }

    private void AddAttributeDefinition(Transaction transaction, BlockTableRecord definition, string tag, Point3d position)
    {
        AttributeDefinition attributeDefinition = new()
        {
            Position = position,
            Tag = tag,
            Prompt = tag,
            TextString = string.Empty,
            Height = 200,
            TextStyleId = _database.Textstyle
        };

        definition.AppendEntity(attributeDefinition);

        transaction.AddNewlyCreatedDBObject(attributeDefinition, true);
    }

    private static ObjectId InsertBlock(
        Transaction transaction,
        BlockTableRecord modelSpace,
        ObjectId blockDefinitionId,
        Point3d position,
        string layerName,
        IReadOnlyDictionary<string, string> attributeValues)
    {
        BlockReference blockReference = new(position, blockDefinitionId) { Layer = layerName };

        ObjectId blockReferenceId = modelSpace.AppendEntity(blockReference);

        transaction.AddNewlyCreatedDBObject(blockReference, true);

        BlockTableRecord blockDefinition = (BlockTableRecord)transaction.GetObject(blockDefinitionId, OpenMode.ForRead);

        foreach (ObjectId objectId in blockDefinition)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not AttributeDefinition attributeDefinition)
            {
                continue;
            }

            if (attributeDefinition.Constant)
            {
                continue;
            }

            AttributeReference attributeReference = new();

            attributeReference.SetAttributeFromBlock(attributeDefinition, blockReference.BlockTransform);

            if (attributeValues.TryGetValue(attributeDefinition.Tag, out string? value))
            {
                attributeReference.TextString = value;
            }

            blockReference.AttributeCollection.AppendAttribute(attributeReference);

            transaction.AddNewlyCreatedDBObject(attributeReference, true);
        }

        return blockReferenceId;
    }

    private void ClearExistingTestEntities(Transaction transaction)
    {
        BlockTableRecord modelSpace = GetModelSpaceForWrite(transaction);

        List<ObjectId> entitiesToDelete = new();

        foreach (ObjectId objectId in modelSpace)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not Entity entity)
            {
                continue;
            }

            bool isTestLayer = string.Equals(entity.Layer, RackLayer, StringComparison.OrdinalIgnoreCase) || string.Equals(entity.Layer, MachineLayer, StringComparison.OrdinalIgnoreCase);

            if (isTestLayer)
            {
                entitiesToDelete.Add(objectId);
            }
        }

        foreach (ObjectId objectId in entitiesToDelete)
        {
            Entity entity = (Entity)transaction.GetObject(objectId, OpenMode.ForWrite);

            entity.Erase();
        }
    }

    private BlockTableRecord GetModelSpaceForWrite(Transaction transaction)
    {
        BlockTable blockTable = (BlockTable)transaction.GetObject(_database.BlockTableId, OpenMode.ForRead);

        return (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
    }
}
