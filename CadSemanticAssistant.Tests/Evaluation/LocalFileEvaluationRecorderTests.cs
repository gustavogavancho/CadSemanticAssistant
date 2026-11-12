using System.Text.Json;
using CadSemanticAssistant.Application.Evaluation;
using CadSemanticAssistant.Domain.Queries;
using CadSemanticAssistant.Domain.Semantics;
using CadSemanticAssistant.Infrastructure.Configuration.Evaluation;

namespace CadSemanticAssistant.Tests.Evaluation;

public sealed class LocalFileEvaluationRecorderTests
{
    [Fact]
    public void Record_WhenNotEnabled_ShouldNotCreateAnyFiles()
    {
        string folder = NewFolder();
        try
        {
            LocalFileEvaluationRecorder recorder = new(folder);
            recorder.Record(CreateExecutionRecord());

            Assert.Equal(0, recorder.RecordedCount);
            Assert.False(Directory.Exists(folder));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public void Record_WhenEnabled_ShouldPersistMinimalAnonymousMetrics()
    {
        string folder = NewFolder();
        try
        {
            LocalFileEvaluationRecorder recorder = new(folder)
            {
                IsEnabled = true
            };
            recorder.Record(CreateExecutionRecord());

            Assert.Equal(1, recorder.RecordedCount);
            string path = Assert.Single(Directory.GetFiles(folder, "*.jsonl"));
            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = json.RootElement;

            Assert.Equal("Execution", root.GetProperty("Stage").GetString());
            Assert.Equal("Rack", root.GetProperty("EntityType").GetString());
            Assert.Equal(3, root.GetProperty("ResultCount").GetInt32());
            Assert.False(root.TryGetProperty("NaturalLanguageText", out _));
            Assert.False(root.TryGetProperty("DrawingPath", out _));
            Assert.False(root.TryGetProperty("ReferenceEntityIds", out _));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public void ExportCsv_ShouldIncludeTechnicalOutcomeAndSeparateHumanFeedback()
    {
        string folder = NewFolder();
        try
        {
            LocalFileEvaluationRecorder recorder = new(folder)
            {
                IsEnabled = true
            };
            Guid trialId = Guid.NewGuid();
            recorder.Record(CreateExecutionRecord() with { TrialId = trialId });
            recorder.Record(new EvaluationRecord
            {
                TrialId = trialId,
                Stage = EvaluationStage.Feedback,
                Success = true,
                UserConfirmedCorrect = false
            });

            string path = recorder.ExportCsv();
            string[] lines = File.ReadAllLines(path);

            Assert.Equal(3, lines.Length);
            Assert.Contains("UserConfirmedCorrect", lines[0]);
            Assert.Contains("Feedback", lines[2]);
            Assert.Contains("False", lines[2]);
            Assert.DoesNotContain("MACHINE_A", File.ReadAllText(path));
        }
        finally
        {
            DeleteFolder(folder);
        }
    }

    [Fact]
    public void ExportCsv_WithoutEvents_ShouldReject()
    {
        LocalFileEvaluationRecorder recorder = new(NewFolder());
        Assert.Throws<InvalidOperationException>(() => recorder.ExportCsv());
    }

    private static EvaluationRecord CreateExecutionRecord()
    {
        return new EvaluationRecord
        {
            TrialId = Guid.NewGuid(),
            Stage = EvaluationStage.Execution,
            Success = true,
            ElapsedMilliseconds = 21,
            EntityType = SemanticEntityType.Rack,
            FilterCount = 1,
            ResultCount = 3,
            ScopeSource = QueryScopeSource.None,
            SpatialRelation = SpatialRelation.WithinDistance
        };
    }

    private static string NewFolder()
    {
        return Path.Combine(Path.GetTempPath(), $"csa-eval-test-{Guid.NewGuid():N}");
    }

    private static void DeleteFolder(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
