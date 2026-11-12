namespace CadSemanticAssistant.Application.Evaluation;

public interface IQueryEvaluationRecorder
{
    /// <summary>Opt-in; false at application startup.</summary>
    public bool IsEnabled { get; set; }

    public int RecordedCount { get; }

    public void Record(EvaluationRecord evaluationRecord);

    /// <summary>Export only this session's explicitly recorded measurements.</summary>
    public string ExportCsv();
}
