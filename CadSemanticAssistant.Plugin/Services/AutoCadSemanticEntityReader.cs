using Autodesk.AutoCAD.DatabaseServices;
using CadSemanticAssistant.Application.Blocks;
using CadSemanticAssistant.Application.Semantics;
using CadSemanticAssistant.Application.Semantics.Configuration;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Infrastructure.AutoCAD.Blocks;

namespace CadSemanticAssistant.Plugin.Services;

public sealed class AutoCadSemanticEntityReader
{
    private readonly SemanticMappingConfiguration _configuration;

    public AutoCadSemanticEntityReader(
        SemanticMappingConfiguration configuration)
    {
        _configuration =
            configuration
            ?? throw new ArgumentNullException(
                nameof(configuration));
    }

    public IReadOnlyCollection<CadEntity> Read(
        Database database)
    {
        ArgumentNullException.ThrowIfNull(
            database);

        ICadBlockReader blockReader =
            new AutoCadBlockReader(
                database);

        IBlockSemanticMapper semanticMapper =
            new RuleBasedCadSemanticMapper(
                _configuration);

        ReadSemanticEntitiesUseCase useCase =
            new(
                blockReader,
                semanticMapper);

        IReadOnlyCollection<SemanticMappingResult> results =
            useCase.Execute();

        return results
            .Select(
                result =>
                    result.Entity)
            .ToArray();
    }
}
