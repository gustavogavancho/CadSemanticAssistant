using System.Diagnostics;
using CadSemanticAssistant.Application.Insertions.Validation;
using CadSemanticAssistant.Domain.Insertions;

namespace CadSemanticAssistant.Application.Benchmarks;

/// <summary>
/// Benchmarks structured semantic-validation inputs (not natural-language
/// interpretation, geometric feasibility, or AutoCAD insertion).
/// </summary>
public sealed class SemanticBenchmarkRunner
{
    private readonly BlockInsertionSemanticValidator _validator;

    public SemanticBenchmarkRunner(BlockInsertionSemanticValidator validator) =>
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));

    public SemanticBenchmarkReport Run(SemanticBenchmarkDataset dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        List<SemanticBenchmarkCaseResult> results = [];
        foreach (SemanticBenchmarkCase testCase in dataset.Cases)
        {
            if (!dataset.SceneSnapshots.TryGetValue(testCase.SceneId,
                    out ExistingBlockSnapshot[]? existing))
            {
                throw new InvalidOperationException(
                    $"Scene '{testCase.SceneId}' missing for benchmark '{testCase.CaseId}'.");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            SemanticValidationResult validation = _validator.Validate(new BlockInsertionBatch
            {
                Existing = existing,
                Proposed = testCase.Proposed
            });
            stopwatch.Stop();

            string actualDecision = validation.IsValid ? "VALID" : "REJECT";
            string[] actualRules = validation.Issues
                .Select(issue => issue.RuleCode)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();
            string[] expectedRules = testCase.ExpectedRuleCodes
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();
            bool passed = string.Equals(actualDecision, testCase.ExpectedDecision,
                    StringComparison.Ordinal) &&
                actualRules.SequenceEqual(expectedRules, StringComparer.Ordinal);

            results.Add(new SemanticBenchmarkCaseResult(
                testCase.CaseId, testCase.SourceScenarioId, testCase.SourceCategory,
                testCase.ExpectedDecision, actualDecision, expectedRules, actualRules,
                stopwatch.Elapsed.TotalMilliseconds, passed,
                validation.Issues.Select(issue => issue.Message).ToArray()));
        }
        return new SemanticBenchmarkReport(dataset.DatasetId, results);
    }
}

public sealed record SemanticBenchmarkCaseResult(
    string CaseId,
    string? SourceScenarioId,
    string SourceCategory,
    string ExpectedDecision,
    string ActualDecision,
    string[] ExpectedRuleCodes,
    string[] ActualRuleCodes,
    double ElapsedMilliseconds,
    bool Passed,
    string[] DiagnosticMessages);

public sealed class SemanticBenchmarkReport
{
    public SemanticBenchmarkReport(string datasetId, IReadOnlyList<SemanticBenchmarkCaseResult> results)
    {
        DatasetId = datasetId;
        Results = results;
    }

    public string DatasetId { get; }
    public IReadOnlyList<SemanticBenchmarkCaseResult> Results { get; }
    public int TotalCases => Results.Count;
    public int PassedCases => Results.Count(result => result.Passed);
    public int FailedCases => TotalCases - PassedCases;
    public bool AllPassed => FailedCases == 0 && TotalCases > 0;
    public double AgreementRate => TotalCases == 0 ? 0 : (double)PassedCases / TotalCases;
    public string[] CoveredExpectedRules => Results
        .SelectMany(result => result.ExpectedRuleCodes)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(rule => rule, StringComparer.Ordinal)
        .ToArray();
}
