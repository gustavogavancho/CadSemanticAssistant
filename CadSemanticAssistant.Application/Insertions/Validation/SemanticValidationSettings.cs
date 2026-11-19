namespace CadSemanticAssistant.Application.Insertions.Validation;

/// <summary>
/// Initial values of SEM-005 and SEM-008 from reglas_dominio.v1.json.
/// The JSON settings loader can override these explicitly.
/// </summary>
public sealed class SemanticValidationSettings
{
    public int MinimumInsertions { get; init; } = 1;
    public int MaximumInsertions { get; init; } = 6;
    public int MaximumComplementsPerTypePerParent { get; init; } = 1;
}
