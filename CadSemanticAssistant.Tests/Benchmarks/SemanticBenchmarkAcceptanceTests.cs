using CadSemanticAssistant.Application.Benchmarks;
using CadSemanticAssistant.Application.Insertions.Validation;
using CadSemanticAssistant.Infrastructure.Configuration.Benchmarks;
using CadSemanticAssistant.Infrastructure.Configuration.Catalog;

namespace CadSemanticAssistant.Tests.Benchmarks;

public sealed class SemanticBenchmarkAcceptanceTests
{
    private static readonly string TestData = Path.Combine(AppContext.BaseDirectory, "TestData");
    private static SemanticBenchmarkDataset Dataset => JsonSemanticBenchmarkDatasetLoader.Load(
        Path.Combine(TestData, "benchmark_semantico.v1.json"));

    public static IEnumerable<object[]> CaseIds => Dataset.Cases
        .Select(testCase => new object[] { testCase.CaseId });

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void BenchmarkCase_MatchesDecisionAndExactRuleCodes(string caseId)
    {
        SemanticBenchmarkDataset dataset = Dataset;
        var selectedCase = Assert.Single(dataset.Cases, testCase => testCase.CaseId == caseId);
        SemanticBenchmarkDataset singleCaseDataset = new()
        {
            SchemaVersion = dataset.SchemaVersion,
            DatasetId = dataset.DatasetId,
            SceneSnapshots = dataset.SceneSnapshots,
            Cases = [selectedCase]
        };
        SemanticBenchmarkReport report = CreateRunner().Run(singleCaseDataset);
        SemanticBenchmarkCaseResult actual = Assert.Single(report.Results);
        Assert.True(actual.Passed,
            $"{caseId}: expected {actual.ExpectedDecision} [{string.Join(",", actual.ExpectedRuleCodes)}], " +
            $"got {actual.ActualDecision} [{string.Join(",", actual.ActualRuleCodes)}]; " +
            string.Join("; ", actual.DiagnosticMessages));
    }

    [Fact]
    public void Dataset_CoversAllEightSemanticRules_AndFourteenSourceCases()
    {
        SemanticBenchmarkDataset dataset = Dataset;
        Assert.Equal(17, dataset.Cases.Length);
        Assert.Equal(14, dataset.Cases.Count(testCase => testCase.SourceScenarioId is not null));
        Assert.Equal(3, dataset.Cases.Count(testCase => testCase.SourceScenarioId is null));
        Assert.Equal(12, dataset.SceneSnapshots["S00_BASE"].Length);
        SemanticBenchmarkReport report = CreateRunner().Run(dataset);
        Assert.Equal(Enumerable.Range(1, 8).Select(i => $"SEM-{i:000}").ToArray(),
            report.CoveredExpectedRules);
        Assert.True(report.AllPassed,
            string.Join(Environment.NewLine, report.Results.Where(result => !result.Passed)
                .Select(result => $"{result.CaseId}: " +
                    $"expected [{string.Join(",", result.ExpectedRuleCodes)}], " +
                    $"got [{string.Join(",", result.ActualRuleCodes)}]")));
    }

    private static SemanticBenchmarkRunner CreateRunner() => new(
        new BlockInsertionSemanticValidator(
            new JsonBlockCatalogProvider(Path.Combine(TestData, "catalogo_bloques.v1.json")),
            JsonSemanticRuleSettingsLoader.Load(Path.Combine(TestData, "reglas_dominio.v1.json"))));
}
