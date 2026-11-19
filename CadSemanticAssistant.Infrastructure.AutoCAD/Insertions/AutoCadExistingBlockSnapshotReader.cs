using CadSemanticAssistant.Application.Blocks;
using CadSemanticAssistant.Domain.Insertions;
using CadSemanticAssistant.Domain.Models;

namespace CadSemanticAssistant.Infrastructure.AutoCAD.Insertions;

/// <summary>
/// Adapts the existing read-only Model Space block reader to the pure semantic
/// validator. No ObjectIds cross the boundary, and no AutoCAD writes occur.
/// </summary>
public sealed class AutoCadExistingBlockSnapshotReader
{
    private readonly ICadBlockReader _blockReader;

    public AutoCadExistingBlockSnapshotReader(ICadBlockReader blockReader)
    {
        _blockReader = blockReader ?? throw new ArgumentNullException(nameof(blockReader));
    }

    public IReadOnlyList<ExistingBlockSnapshot> ReadModelSpace()
    {
        List<ExistingBlockSnapshot> snapshots = [];
        foreach (CadBlock block in _blockReader.ReadModelSpaceBlocks())
        {
            Dictionary<string, string?> attributes = new(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> attribute in block.Attributes)
            {
                attributes[attribute.Key] = attribute.Value;
            }

            snapshots.Add(new ExistingBlockSnapshot
            {
                BlockName = block.Name, // effective name, including dynamic blocks
                Attributes = attributes
            });
        }

        return snapshots;
    }
}
