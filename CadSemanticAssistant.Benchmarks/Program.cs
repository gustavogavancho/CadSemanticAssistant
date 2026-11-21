using System.Globalization;
using System.Text;
using System.Text.Json;
using CadSemanticAssistant.Application.Benchmarks;
using CadSemanticAssistant.Application.Insertions.Validation;
using CadSemanticAssistant.Infrastructure.Configuration.Benchmarks;
using CadSemanticAssistant.Infrastructure.Configuration.Catalog;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Dictionary<string, string> options = ReadOptions(args);
            string datasetPath = Required(options, "--dataset");
            string catalogPath = Required(options, "--catalog");
            string rulesPath = Required(options, "--rules");
            string outDirectory = Required(options, "--out");

            SemanticBenchmarkDataset dataset = JsonSemanticBenchmarkDatasetLoader.Load(datasetPath);
            BlockInsertionSemanticValidator validator = new(
                new JsonBlockCatalogProvider(catalogPath),
                JsonSemanticRuleSettingsLoader.Load(rulesPath));
            SemanticBenchmarkReport report = new SemanticBenchmarkRunner(validator).Run(dataset);

            Directory.CreateDirectory(outDirectory);
            string csvPath = Path.Combine(outDirectory, "semantic_benchmark_results.csv");
            string jsonPath = Path.Combine(outDirectory, "semantic_benchmark_results.json");
            WriteCsv(csvPath, report);
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(report,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }), new UTF8Encoding(false));

            Console.WriteLine($"Dataset: {report.DatasetId}");
            Console.WriteLine($"Cases: {report.TotalCases}; passed: {report.PassedCases}; failed: {report.FailedCases}");
            Console.WriteLine($"Rule coverage: {string.Join(", ", report.CoveredExpectedRules)}");
            Console.WriteLine($"Reports: {csvPath} | {jsonPath}");
            foreach (SemanticBenchmarkCaseResult failed in report.Results.Where(x => !x.Passed))
            {
                Console.Error.WriteLine($"FAIL {failed.CaseId}: expected " +
                    $"{failed.ExpectedDecision} [{string.Join(",", failed.ExpectedRuleCodes)}] " +
                    $"got {failed.ActualDecision} [{string.Join(",", failed.ActualRuleCodes)}]");
            }
            return report.AllPassed ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine("Usage: --dataset <json> --catalog <json> --rules <json> --out <directory>");
            return 2;
        }
    }

    private static Dictionary<string, string> ReadOptions(string[] args)
    {
        if (args.Length == 0 || args.Length % 2 != 0)
        {
            throw new ArgumentException("Four option/value pairs are required.");
        }

        Dictionary<string, string> options = new(StringComparer.Ordinal);

        for (int i = 0; i < args.Length; i += 2)
        {
            if (!new[] { "--dataset", "--catalog", "--rules", "--out" }.Contains(args[i]) ||
                !options.TryAdd(args[i], args[i + 1]))
            {
                throw new ArgumentException($"Unknown or repeated option '{args[i]}'.");
            }
        }

        return options;
    }

    private static string Required(Dictionary<string, string> options, string key) =>
        options.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException($"Missing {key}.");

    private static void WriteCsv(string path, SemanticBenchmarkReport report)
    {
        StringBuilder sb = new();
        sb.AppendLine("caseId;sourceScenarioId;category;expectedDecision;actualDecision;expectedRuleCodes;actualRuleCodes;elapsedMs;passed");
        foreach (SemanticBenchmarkCaseResult result in report.Results)
        {
            string[] fields =
            [
                result.CaseId,
                result.SourceScenarioId ?? string.Empty,
                result.SourceCategory,
                result.ExpectedDecision,
                result.ActualDecision,
                string.Join(",", result.ExpectedRuleCodes),
                string.Join(",", result.ActualRuleCodes),
                result.ElapsedMilliseconds.ToString("F4", CultureInfo.InvariantCulture),
                result.Passed ? "true" : "false"
            ];
            sb.AppendLine(string.Join(";", fields.Select(EscapeCsv)));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string EscapeCsv(string field) =>
        field.Contains(';') || field.Contains('"') || field.Contains('\n') || field.Contains('\r')
            ? "\"" + field.Replace("\"", "\"\"") + "\"" : field;
}
