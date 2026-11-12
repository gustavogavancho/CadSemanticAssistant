using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CadSemanticAssistant.Application.Evaluation;

namespace CadSemanticAssistant.Infrastructure.Configuration.Evaluation;

/// <summary>
/// Local, opt-in metrics collection. Data is anonymous and never transmitted.
/// Each host lifetime has a new session ID; there is no persistent user ID.
/// </summary>
public sealed class LocalFileEvaluationRecorder : IQueryEvaluationRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _sync = new();
    private readonly List<EvaluationRecord> _records = [];
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly string _directory;

    public LocalFileEvaluationRecorder(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CadSemanticAssistant",
            "Evaluation");
    }

    public bool IsEnabled { get; set; }

    public int RecordedCount
    {
        get
        {
            lock (_sync)
            {
                return _records.Count;
            }
        }
    }

    public void Record(EvaluationRecord evaluationRecord)
    {
        ArgumentNullException.ThrowIfNull(evaluationRecord);
        if (!IsEnabled)
        {
            return;
        }

        EvaluationRecord sessionRecord = evaluationRecord with
        {
            SessionId = _sessionId
        };

        lock (_sync)
        {
            Directory.CreateDirectory(_directory);
            string path = Path.Combine(_directory, $"session-{_sessionId:N}.jsonl");
            string line = JsonSerializer.Serialize(sessionRecord, JsonOptions);

            // Only count persisted records; failed writes do not corrupt the count.
            File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            _records.Add(sessionRecord);
        }
    }

    public string ExportCsv()
    {
        lock (_sync)
        {
            if (_records.Count == 0)
            {
                throw new InvalidOperationException(
                    "No evaluation measurements recorded in this session.");
            }

            Directory.CreateDirectory(_directory);
            string path = Path.Combine(_directory, $"evaluation-{_sessionId:N}.csv");
            List<string> lines =
            [
                "SessionId,TrialId,TimestampUtc,Stage,Success,ElapsedMilliseconds,EntityType,FilterCount,ResultCount,ScopeSource,SpatialRelation,FailureKind,UserConfirmedCorrect"
            ];

            foreach (EvaluationRecord evaluationRecord in _records)
            {
                string[] fields =
                [
                    evaluationRecord.SessionId.ToString("N"),
                    evaluationRecord.TrialId.ToString("N"),
                    evaluationRecord.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                    evaluationRecord.Stage.ToString(),
                    evaluationRecord.Success ? "true" : "false",
                    evaluationRecord.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture),
                    evaluationRecord.EntityType?.ToString() ?? string.Empty,
                    evaluationRecord.FilterCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    evaluationRecord.ResultCount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    evaluationRecord.ScopeSource?.ToString() ?? string.Empty,
                    evaluationRecord.SpatialRelation?.ToString() ?? string.Empty,
                    evaluationRecord.FailureKind.ToString(),
                    evaluationRecord.UserConfirmedCorrect?.ToString() ?? string.Empty
                ];

                lines.Add(string.Join(",", fields.Select(EscapeCsv)));
            }

            File.WriteAllLines(path, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            return path;
        }
    }

    private static string EscapeCsv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
