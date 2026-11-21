#pragma warning disable IDE0005 // Using directive is unnecessary.
using System;
using System.Collections.Generic;
using System.Text;
#pragma warning restore IDE0005 // Using directive is unnecessary.
using System.Text.Json;
using CadSemanticAssistant.Application.Benchmarks;

namespace CadSemanticAssistant.Infrastructure.Configuration.Benchmarks;

/// <summary>Loads and validates the experimental fixture contract.</summary>
public static class JsonSemanticBenchmarkDatasetLoader
{
    public static SemanticBenchmarkDataset Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Benchmark dataset not found.", path);
        }

        SemanticBenchmarkDataset dataset;
        try
        {
            dataset = JsonSerializer.Deserialize<SemanticBenchmarkDataset>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("Empty benchmark dataset.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Invalid semantic benchmark JSON.", ex);
        }

        if (dataset.SchemaVersion != "1.0.0" ||
            string.IsNullOrWhiteSpace(dataset.DatasetId) ||
            dataset.SceneSnapshots is null || dataset.Cases is null ||
            dataset.SceneSnapshots.Count == 0 || dataset.Cases.Length == 0)
        {
            throw new InvalidDataException("Unsupported or empty benchmark dataset.");
        }

        HashSet<string> caseIds = new(StringComparer.Ordinal);
        foreach (SemanticBenchmarkCase testCase in dataset.Cases)
        {
            if (testCase is null || string.IsNullOrWhiteSpace(testCase.CaseId) ||
                !caseIds.Add(testCase.CaseId) || string.IsNullOrWhiteSpace(testCase.SceneId) ||
                !dataset.SceneSnapshots.TryGetValue(testCase.SceneId, out var scene) ||
                scene is null || testCase.Proposed is null ||
                testCase.ExpectedRuleCodes is null ||
                (testCase.ExpectedDecision != "VALID" &&
                 testCase.ExpectedDecision != "REJECT") ||
                (testCase.ExpectedDecision == "VALID" && testCase.ExpectedRuleCodes.Length > 0) ||
                (testCase.ExpectedDecision == "REJECT" && testCase.ExpectedRuleCodes.Length == 0) ||
                testCase.ExpectedRuleCodes.Any(code => !IsSemanticCode(code)))
            {
                throw new InvalidDataException(
                    $"Malformed or unsupported benchmark case: '{testCase?.CaseId}'.");
            }
        }
        return dataset;
    }

    private static bool IsSemanticCode(string? code) =>
        code is "SEM-001" or "SEM-002" or "SEM-003" or "SEM-004" or
            "SEM-005" or "SEM-006" or "SEM-007" or "SEM-008";
}
