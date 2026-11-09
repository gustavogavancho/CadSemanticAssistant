namespace CadSemanticAssistant.Application.Interaction;

public interface IInteractionContextProvider
{
    public InteractionContext GetCurrentContext();
}
