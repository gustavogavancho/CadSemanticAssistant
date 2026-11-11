using CadSemanticAssistant.Domain.Queries;

namespace CadSemanticAssistant.Application.Conversation;

/// <summary>Snapshot of the last EXECUTED query in one drawing.</summary>
public sealed class ConversationContext
{
    public static ConversationContext Empty { get; } = new();

    // Identifies one open drawing, not the disk path or title.
    public Guid DrawingToken { get; init; }

    // Incremented after each execution or reset. Used to reject stale async replies.
    public long Revision { get; init; }

    public SemanticQuery? PreviousQuery { get; init; }

    public IReadOnlyCollection<string> PreviousResultEntityIds { get; init; } = [];

    public bool HasPreviousQuery => PreviousQuery is not null;

    public bool HasPreviousResults => PreviousResultEntityIds.Count > 0;
}
