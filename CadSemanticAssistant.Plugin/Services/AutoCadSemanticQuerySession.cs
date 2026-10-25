using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

using CadSemanticAssistant.Application.Blocks;
using CadSemanticAssistant.Application.Queries;
using CadSemanticAssistant.Application.Queries.Validation;
using CadSemanticAssistant.Application.Selection;
using CadSemanticAssistant.Application.Semantics;
using CadSemanticAssistant.Application.Semantics.Configuration;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Queries;
using CadSemanticAssistant.Infrastructure.AutoCAD.Blocks;
using CadSemanticAssistant.Infrastructure.AutoCAD.Selection;
using CadSemanticAssistant.Infrastructure.Configuration.Semantics;
using CadSemanticAssistant.Application.Queries.Validation;

namespace CadSemanticAssistant.Plugin.Services;

public sealed class AutoCadSemanticQuerySession : ISemanticQuerySession
{
    private readonly string _configurationPath;

    private Document? _lastQueryDocument;

    private readonly ISemanticQueryValidator _queryValidator;

    public AutoCadSemanticQuerySession(
        string configurationPath,
        ISemanticQueryValidator queryValidator)
    {
        _configurationPath =
            configurationPath;

        _queryValidator =
            queryValidator;
    }

    public SemanticQueryResult Execute(
        SemanticQuery query)
    {
        Document document =
            GetActiveDocument();

        using DocumentLock documentLock =
            document.LockDocument();

        IReadOnlyCollection<CadEntity> entities =
            ReadSemanticEntities(
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

        _lastQueryDocument =
            document;

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
