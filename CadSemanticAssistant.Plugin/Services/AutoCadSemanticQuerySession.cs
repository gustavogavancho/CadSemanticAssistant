using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using CadSemanticAssistant.Application.Blocks;
using CadSemanticAssistant.Application.Queries;
using CadSemanticAssistant.Application.Selection;
using CadSemanticAssistant.Application.Semantics;
using CadSemanticAssistant.Application.Semantics.Configuration;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Queries;
using CadSemanticAssistant.Infrastructure.AutoCAD.Blocks;
using CadSemanticAssistant.Infrastructure.AutoCAD.Selection;
using CadSemanticAssistant.Infrastructure.Configuration.Semantics;

namespace CadSemanticAssistant.Plugin.Services;

public sealed class AutoCadSemanticQuerySession : ISemanticQuerySession
{
    private readonly string _configurationPath;

    private Document? _lastQueryDocument;

    public AutoCadSemanticQuerySession(string configurationPath)
    {
        _configurationPath = configurationPath;
    }

    public SemanticQueryResult Execute(SemanticQuery query)
    {
        Document document = GetActiveDocument();

        /*
         * The PaletteSet is modeless.
         * Therefore we explicitly lock the current document
         * before interacting with AutoCAD.
         */
        using DocumentLock documentLock = document.LockDocument();

        IReadOnlyCollection<CadEntity> entities = ReadSemanticEntities(document.Database);

        ISemanticQueryEngine queryEngine = new SemanticQueryEngine();

        SemanticQueryResult result = queryEngine.Execute(query, entities);

        _lastQueryDocument = document;

        return result;
    }

    public CadSelectionResult Select(IReadOnlyCollection<CadEntity> entities)
    {
        Document document = GetActiveDocument();

        if (_lastQueryDocument is not null && !ReferenceEquals(_lastQueryDocument, document))
        {
            throw new InvalidOperationException(
                "The active drawing has changed since the query was executed. " +
                "Execute the query again before selecting its results.");
        }

        using DocumentLock documentLock = document.LockDocument();

        ICadEntitySelectionService selectionService = new AutoCadEntitySelectionService(document.Database, document.Editor);

        return selectionService.Select(entities);
    }

    public void ClearSelection()
    {
        Document document = GetActiveDocument();

        using DocumentLock documentLock = document.LockDocument();

        ICadEntitySelectionService selectionService = new AutoCadEntitySelectionService(document.Database, document.Editor);

        selectionService.ClearSelection();
    }

    private IReadOnlyCollection<CadEntity> ReadSemanticEntities(Database database)
    {
        ICadBlockReader blockReader = new AutoCadBlockReader(database);

        ISemanticMappingConfigurationProvider configurationProvider = new JsonSemanticMappingConfigurationProvider(_configurationPath);

        SemanticMappingConfiguration configuration = configurationProvider.GetConfiguration();

        IBlockSemanticMapper semanticMapper = new RuleBasedCadSemanticMapper(configuration);

        ReadSemanticEntitiesUseCase useCase = new(blockReader, semanticMapper);

        IReadOnlyCollection<SemanticMappingResult> mappingResults = useCase.Execute();

        return mappingResults .Select(result => result.Entity).ToArray();
    }

    private static Document GetActiveDocument()
    {
        return Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument ??
            throw new InvalidOperationException("There is no active AutoCAD drawing.");
    }
}
