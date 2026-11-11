using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using CadSemanticAssistant.Application.Conversation;
using CadSemanticAssistant.Application.Queries;
using CadSemanticAssistant.Domain.Queries;

namespace CadSemanticAssistant.Plugin.Services;

/// <summary>In-memory last-result context, isolated per open Document.</summary>
public sealed class AutoCadConversationContextStore : IConversationContextStore
{
    private readonly ConditionalWeakTable<Document, DrawingState> _states = new();

    public ConversationContext GetCurrentContext()
    {
        DrawingState state = GetState();
        return new ConversationContext
        {
            DrawingToken = state.DrawingToken,
            Revision = state.Revision,
            PreviousQuery = state.PreviousQuery,
            PreviousResultEntityIds = state.ResultIds.ToArray()
        };
    }

    public void Remember(SemanticQueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        DrawingState state = GetState();
        state.PreviousQuery = result.Query;
        state.ResultIds = result.Entities
            .Select(entity => entity.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        state.Revision++;
    }

    public void Clear()
    {
        DrawingState state = GetState();
        state.PreviousQuery = null;
        state.ResultIds = [];
        state.Revision++;
    }

    private DrawingState GetState()
    {
        Document document = Autodesk.AutoCAD.ApplicationServices.Application
            .DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("There is no active AutoCAD drawing.");

        return _states.GetValue(document, _ => new DrawingState());
    }

    private sealed class DrawingState
    {
        public Guid DrawingToken { get; } = Guid.NewGuid();
        public long Revision { get; set; }
        public SemanticQuery? PreviousQuery { get; set; }
        public IReadOnlyCollection<string> ResultIds { get; set; } = [];
    }
}
