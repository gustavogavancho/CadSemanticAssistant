using Autodesk.AutoCAD.ApplicationServices;
using CadSemanticAssistant.Application.Conversation;
using CadSemanticAssistant.Application.Queries;
using CadSemanticAssistant.Application.Queries.Validation;
using CadSemanticAssistant.Application.Selection;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Queries;
using CadSemanticAssistant.Infrastructure.AutoCAD.Selection;

namespace CadSemanticAssistant.Plugin.Services;

public sealed class AutoCadSemanticQuerySession
    : ISemanticQuerySession
{
    private readonly AutoCadSemanticEntityReader
        _semanticEntityReader;

    private readonly ISemanticQueryValidator
        _queryValidator;

    private readonly IConversationContextStore _conversationContextStore;

    private Document? _lastQueryDocument;

    public AutoCadSemanticQuerySession(
        AutoCadSemanticEntityReader semanticEntityReader,
        ISemanticQueryValidator queryValidator,
        IConversationContextStore conversationContextStore)
    {
        _semanticEntityReader =
            semanticEntityReader
            ?? throw new ArgumentNullException(
                nameof(semanticEntityReader));

        _queryValidator =
            queryValidator
            ?? throw new ArgumentNullException(nameof(queryValidator));

        _conversationContextStore = conversationContextStore
            ?? throw new ArgumentNullException(nameof(conversationContextStore));
    }

    public SemanticQueryResult Execute(
        SemanticQuery query)
    {
        Document document =
            GetActiveDocument();

        using DocumentLock documentLock =
            document.LockDocument();

        IReadOnlyCollection<CadEntity> entities =
            _semanticEntityReader.Read(
                document.Database);

        ISemanticQueryEngine queryEngine =
            new SemanticQueryEngine();

        ExecuteSemanticQueryUseCase useCase =
            new(
                _queryValidator,
                queryEngine);

        SemanticQueryResult result =
            useCase.Execute(
                query,
                entities);

        _lastQueryDocument = document;

        // Commit only successfully validated and executed results.
        // A zero-result query also replaces the preceding context.
        _conversationContextStore.Remember(result);

        return result;
    }

    public CadSelectionResult Select(
        IReadOnlyCollection<CadEntity> entities)
    {
        Document document =
            GetActiveDocument();

        if (_lastQueryDocument is not null &&
            !ReferenceEquals(
                _lastQueryDocument,
                document))
        {
            throw new InvalidOperationException(
                "The active drawing has changed since the query was executed. " +
                "Execute the query again before selecting its results.");
        }

        using DocumentLock documentLock =
            document.LockDocument();

        ICadEntitySelectionService selectionService =
            new AutoCadEntitySelectionService(
                document.Database,
                document.Editor);

        return selectionService.Select(
            entities);
    }

    public void ClearSelection()
    {
        Document document =
            GetActiveDocument();

        using DocumentLock documentLock =
            document.LockDocument();

        ICadEntitySelectionService selectionService =
            new AutoCadEntitySelectionService(
                document.Database,
                document.Editor);

        selectionService.ClearSelection();
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
