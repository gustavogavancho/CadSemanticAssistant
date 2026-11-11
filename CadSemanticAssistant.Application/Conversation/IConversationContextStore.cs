using CadSemanticAssistant.Application.Queries;

namespace CadSemanticAssistant.Application.Conversation;

public interface IConversationContextStore
{
    public ConversationContext GetCurrentContext();

    // Called only after successful query execution.
    public void Remember(SemanticQueryResult result);

    // Clears only the current drawing's conversation, not AutoCAD's selection.
    public void Clear();
}
