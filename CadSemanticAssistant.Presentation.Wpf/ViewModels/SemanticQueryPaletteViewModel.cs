using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using CadSemanticAssistant.Application.Conversation;
using CadSemanticAssistant.Application.Evaluation;
using CadSemanticAssistant.Application.Interaction;
using CadSemanticAssistant.Application.NaturalLanguage;
using CadSemanticAssistant.Application.Queries;
using CadSemanticAssistant.Application.Queries.Validation;
using CadSemanticAssistant.Application.Selection;
using CadSemanticAssistant.Application.Semantics;
using CadSemanticAssistant.Application.Semantics.Configuration;
using CadSemanticAssistant.Domain.Models;
using CadSemanticAssistant.Domain.Queries;
using CadSemanticAssistant.Domain.Semantics;

namespace CadSemanticAssistant.Presentation.Wpf.ViewModels;

public sealed class SemanticQueryPaletteViewModel : ObservableObject
{
    private readonly ISemanticQuerySession _querySession;
    private readonly SemanticMappingConfiguration _configuration;
    private readonly IInteractionContextProvider _interactionContextProvider;

    private IReadOnlyCollection<string> _scopeEntityIds = [];
    private SpatialFilter? _spatialFilter;
    private QueryScopeSource _scopeSource = QueryScopeSource.None;
    private QueryIntent _queryIntent = QueryIntent.Select;
    private Guid _queryDrawingToken;
    private readonly IConversationContextStore _conversationContextStore;

    private SemanticEntityType _selectedEntityType;

    private SemanticQueryResult? _lastResult;

    private string _statusText = "Ready.";

    private readonly INaturalLanguageQueryInterpreter _naturalLanguageQueryInterpreter;

    private string _naturalLanguageText = string.Empty;

    private bool _isInterpreting;

    private readonly ISemanticQueryValidator _queryValidator;
    private readonly IQueryEvaluationRecorder _evaluationRecorder;
    private string _evaluationStatusText = "Evaluation: off (opt in to collect metrics).";
    private Guid _pendingTrialId = Guid.Empty;
    private Guid _lastExecutedTrialId = Guid.Empty;
    private bool _hasFeedbackForLastResult;
    private bool _lastExecutionWasRecorded;

    public SemanticQueryPaletteViewModel(
        ISemanticQuerySession querySession,
        SemanticMappingConfiguration configuration,
        INaturalLanguageQueryInterpreter naturalLanguageQueryInterpreter,
        ISemanticQueryValidator queryValidator,
        IInteractionContextProvider interactionContextProvider,
        IConversationContextStore conversationContextStore,
        IQueryEvaluationRecorder evaluationRecorder)
    {
        _evaluationRecorder = evaluationRecorder
            ?? throw new ArgumentNullException(nameof(evaluationRecorder));

        _querySession =
            querySession;

        _configuration =
            configuration;

        _naturalLanguageQueryInterpreter =
            naturalLanguageQueryInterpreter;

        _queryValidator = queryValidator;

        _interactionContextProvider = interactionContextProvider;
        _conversationContextStore = conversationContextStore
            ?? throw new ArgumentNullException(nameof(conversationContextStore));

        EntityTypes =
            new ObservableCollection<SemanticEntityType>(
                configuration.EntityMappings
                    .Select(mapping => mapping.EntityType)
                    .Distinct());

        Filters = [];

        Results = [];

        AddFilterCommand =
            new RelayCommand(
                _ => AddFilter());

        RemoveFilterCommand =
            new RelayCommand(
                RemoveFilter);

        ExecuteCommand =
            new RelayCommand(
                _ => ExecuteQuery());

        SelectResultsCommand =
            new RelayCommand(
                _ => SelectResults(),
                _ => _lastResult?.Count > 0);

        ClearSelectionCommand =
            new RelayCommand(
                _ => ClearSelection());

        ResetConversationCommand = new RelayCommand(
            _ => ResetConversation());

        ExportEvaluationCommand = new RelayCommand(_ => ExportEvaluation());
        MarkResultExpectedCommand = new RelayCommand(
            _ => RecordFeedback(true), _ => CanRecordFeedback());
        MarkResultUnexpectedCommand = new RelayCommand(
            _ => RecordFeedback(false), _ => CanRecordFeedback());

        InterpretNaturalLanguageCommand =
            new AsyncRelayCommand(
                InterpretNaturalLanguageAsync);

        SelectedEntityType =
            EntityTypes.FirstOrDefault();

        AddFilter();
    }

    public ObservableCollection<SemanticEntityType> EntityTypes { get; }

    public ObservableCollection<QueryFilterRowViewModel> Filters { get; }

    public ObservableCollection<QueryResultItemViewModel> Results { get; }

    public SemanticEntityType SelectedEntityType
    {
        get => _selectedEntityType;

        set
        {
            if (!SetProperty(ref _selectedEntityType, value))
            {
                return;
            }

            RefreshFilterProperties();
        }
    }

    public string StatusText
    {
        get => _statusText;

        private set => SetProperty(ref _statusText, value);
    }

    public string NaturalLanguageText
    {
        get => _naturalLanguageText;

        set => SetProperty(
            ref _naturalLanguageText,
            value);
    }

    public bool IsInterpreting
    {
        get => _isInterpreting;

        private set => SetProperty(
            ref _isInterpreting,
            value);
    }

    public AsyncRelayCommand InterpretNaturalLanguageCommand
    {
        get;
    }

    public int ResultCount => Results.Count;

    public int ScopeEntityCount => _scopeEntityIds.Count;

    public string ScopeText => _scopeEntityIds.Count == 0
        ? "Scope: entire drawing"
        : _scopeSource == QueryScopeSource.PreviousResults
            ? $"Scope: {_scopeEntityIds.Count} previous result(s)"
            : $"Scope: {_scopeEntityIds.Count} selected entity(s)";

    public string ConversationStatusText
    {
        get
        {
            ConversationContext context;
            try
            {
                context = _conversationContextStore.GetCurrentContext();
            }
            catch (InvalidOperationException)
            {
                return "Conversation: no active drawing";
            }

            if (!context.HasPreviousQuery)
            {
                return "Conversation: no previous executed query";
            }

            return $"Conversation: {context.PreviousResultEntityIds.Count} previous " +
                   $"{context.PreviousQuery!.EntityType} result(s)";
        }
    }

    public string SpatialContextText => _spatialFilter?.Relation switch
    {
        null => "Spatial relation: none",
        SpatialRelation.WithinDistance =>
            $"Spatial relation: within {_spatialFilter.MaxDistance:0.##} mm " +
            "of selected reference",
        SpatialRelation.Nearest =>
            "Spatial relation: nearest to selected reference",
        _ => "Spatial relation: unknown"
    };

    public RelayCommand AddFilterCommand { get; }

    public RelayCommand RemoveFilterCommand { get; }

    public RelayCommand ExecuteCommand { get; }

    public RelayCommand SelectResultsCommand { get; }

    public RelayCommand ClearSelectionCommand { get; }

    public RelayCommand ResetConversationCommand { get; }

    public RelayCommand ExportEvaluationCommand { get; }

    public RelayCommand MarkResultExpectedCommand { get; }

    public RelayCommand MarkResultUnexpectedCommand { get; }

    public bool IsEvaluationEnabled
    {
        get => _evaluationRecorder.IsEnabled;
        set
        {
            if (_evaluationRecorder.IsEnabled == value)
            {
                return;
            }

            _evaluationRecorder.IsEnabled = value;
            OnPropertyChanged(nameof(IsEvaluationEnabled));
            EvaluationStatusText = value
                ? "Evaluation: recording local anonymous metrics."
                : "Evaluation: off. Previous recorded data remains local.";
            RefreshFeedbackCommands();
        }
    }

    public string EvaluationStatusText
    {
        get => _evaluationStatusText;
        private set => SetProperty(ref _evaluationStatusText, value);
    }

    private void AddFilter()
    {
        QueryFilterRowViewModel filter = new();

        filter.SetProperties(GetAvailableProperties());

        Filters.Add(filter);
    }

    private void RemoveFilter(object? parameter)
    {
        if (parameter is not QueryFilterRowViewModel filter)
        {
            return;
        }

        Filters.Remove(filter);
    }

    private void ExecuteQuery()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Guid trialId = _pendingTrialId == Guid.Empty
            ? Guid.NewGuid()
            : _pendingTrialId;
        _pendingTrialId = Guid.Empty;
        SemanticQuery? query = null;

        // Feedback must correspond to this attempt only.
        _lastExecutionWasRecorded = false;
        _hasFeedbackForLastResult = false;
        _lastExecutedTrialId = Guid.Empty;
        RefreshFeedbackCommands();

        try
        {
            ConversationContext active = _conversationContextStore.GetCurrentContext();
            OnPropertyChanged(nameof(ConversationStatusText));
            if (_queryDrawingToken != Guid.Empty &&
                _queryDrawingToken != active.DrawingToken)
            {
                throw new InvalidOperationException(
                    "The drawing changed after interpretation. Interpret again.");
            }

            query = BuildQuery();
            SemanticQueryResult result = _querySession.Execute(query);

            _lastResult = result;
            _queryDrawingToken = active.DrawingToken;
            _lastExecutedTrialId = trialId;
            OnPropertyChanged(nameof(ConversationStatusText));

            Results.Clear();
            foreach (CadEntity entity in result.Entities)
            {
                Results.Add(new QueryResultItemViewModel(entity));
            }

            OnPropertyChanged(nameof(ResultCount));
            SelectResultsCommand.RaiseCanExecuteChanged();

            stopwatch.Stop();
            _lastExecutionWasRecorded = TryRecordEvaluation(
                trialId, EvaluationStage.Execution, true, stopwatch.ElapsedMilliseconds,
                query, result.Count, EvaluationFailureKind.None);
            RefreshFeedbackCommands();
            StatusText = $"Query executed successfully. {result.Count} result(s).";
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            TryRecordEvaluation(
                trialId, EvaluationStage.Execution, false, stopwatch.ElapsedMilliseconds,
                query, null, ClassifyFailure(exception));

            _lastResult = null;
            Results.Clear();
            OnPropertyChanged(nameof(ResultCount));
            SelectResultsCommand.RaiseCanExecuteChanged();
            RefreshFeedbackCommands();
            StatusText = exception.Message;
        }
    }

    private void SelectResults()
    {
        if (_lastResult is null)
        {
            return;
        }

        try
        {
            CadSelectionResult result = _querySession.Select(_lastResult.Entities);

            StatusText = $"Selected {result.SelectedCount} of {result.RequestedCount} result(s).";
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    private void ClearSelection()
    {
        try
        {
            _querySession.ClearSelection();

            StatusText = "AutoCAD selection cleared.";
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    private void ResetConversation()
    {
        try
        {
            _conversationContextStore.Clear();
            _lastResult = null;
            _queryDrawingToken = Guid.Empty;
            _scopeEntityIds = [];
            _scopeSource = QueryScopeSource.None;
            _spatialFilter = null;
            _queryIntent = QueryIntent.Select;
            _pendingTrialId = Guid.Empty;
            _lastExecutedTrialId = Guid.Empty;
            _lastExecutionWasRecorded = false;
            _hasFeedbackForLastResult = false;
            RefreshFeedbackCommands();
            NaturalLanguageText = string.Empty;
            Filters.Clear();
            AddFilter();
            Results.Clear();
            OnPropertyChanged(nameof(ResultCount));
            OnPropertyChanged(nameof(ScopeEntityCount));
            OnPropertyChanged(nameof(ScopeText));
            OnPropertyChanged(nameof(SpatialContextText));
            OnPropertyChanged(nameof(ConversationStatusText));
            SelectResultsCommand.RaiseCanExecuteChanged();
            StatusText = "Conversation cleared for the active drawing. AutoCAD selection unchanged.";
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    private SemanticQuery BuildQuery()
    {
        List<QueryFilter> queryFilters = [];

        foreach (QueryFilterRowViewModel filter in Filters)
        {
            if (filter.SelectedProperty is null)
            {
                throw new InvalidOperationException("Select a property for every filter.");
            }

            if (filter.SelectedOperator is null)
            {
                throw new InvalidOperationException("Select an operator for every filter.");
            }

            if (string.IsNullOrWhiteSpace(filter.Value))
            {
                throw new InvalidOperationException($"Enter a value for '{filter.SelectedProperty.Name}'.");
            }

            object value = ParseValue(filter.Value, filter.SelectedProperty.DataType);

            queryFilters.Add(new QueryFilter
            {
                Property = filter.SelectedProperty.Name,

                Operator = filter.SelectedOperator.Value,

                Value = value
            });
        }

        return new SemanticQuery
        {
            Intent = _queryIntent,

            EntityType = SelectedEntityType,

            Filters = queryFilters,

            ScopeEntityIds = _scopeEntityIds,

            ScopeSource = _scopeSource,

            SpatialFilter = _spatialFilter
        };
    }

    private IEnumerable<QueryPropertyOption> GetAvailableProperties()
    {
        return _configuration
            .EntityMappings
            .Where(
                mapping =>
                    mapping.EntityType ==
                    SelectedEntityType)
            .SelectMany(
                mapping =>
                    mapping.Properties)
            .GroupBy(
                property =>
                    property.TargetProperty,
                StringComparer.OrdinalIgnoreCase)
            .Select(
                group =>
                {
                    PropertyMappingRule property =
                        group.First();

                    return new QueryPropertyOption
                    {
                        Name =
                            property.TargetProperty,

                        DataType =
                            property.DataType,

                        Unit =
                            property.Unit
                    };
                })
            .OrderBy(
                property =>
                    property.Name);
    }

    private void RefreshFilterProperties()
    {
        QueryPropertyOption[] properties = GetAvailableProperties().ToArray();

        foreach (QueryFilterRowViewModel filter in Filters)
        {
            filter.SetProperties(properties);
        }
    }

    private static object ParseValue(string text, SemanticDataType dataType)
    {
        return dataType switch
        {
            SemanticDataType.String => text,

            SemanticDataType.Integer => ParseInteger(text),

            SemanticDataType.Double => ParseDouble(text),

            SemanticDataType.Boolean => ParseBoolean(text),

            _ => throw new InvalidOperationException($"Unsupported data type: {dataType}.")
        };
    }

    private static int ParseInteger(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int result))
        {
            return result;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
        {
            return result;
        }

        throw new FormatException($"'{value}' is not a valid integer.");
    }

    private static double ParseDouble(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out double result))
        {
            return result;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
        {
            return result;
        }

        throw new FormatException($"'{value}' is not a valid number.");
    }

    private static bool ParseBoolean(string value)
    {
        if (bool.TryParse(value, out bool result))
        {
            return result;
        }

        throw new FormatException($"'{value}' is not a valid Boolean value.");
    }

    private async Task InterpretNaturalLanguageAsync()
    {
        if (string.IsNullOrWhiteSpace(NaturalLanguageText))
        {
            StatusText = "Enter a natural-language query.";
            return;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        Guid trialId = Guid.NewGuid();
        _pendingTrialId = Guid.Empty;
        // A new interpretation closes feedback for the preceding execution.
        _hasFeedbackForLastResult = true;
        RefreshFeedbackCommands();
        SemanticQuery? query = null;

        try
        {
            IsInterpreting = true;
            StatusText = "Reading AutoCAD context...";

            ConversationContext conversationContext =
                _conversationContextStore.GetCurrentContext();
            OnPropertyChanged(nameof(ConversationStatusText));
            InteractionContext context = _interactionContextProvider.GetCurrentContext();

            StatusText = "Interpreting query...";
            query = await _naturalLanguageQueryInterpreter.InterpretAsync(
                NaturalLanguageText, context, conversationContext);

            ConversationContext currentContext =
                _conversationContextStore.GetCurrentContext();
            if (currentContext.DrawingToken != conversationContext.DrawingToken ||
                currentContext.Revision != conversationContext.Revision)
            {
                throw new InvalidOperationException(
                    "The active drawing/conversation changed during interpretation. Try again.");
            }

            SemanticQueryValidationResult validationResult = _queryValidator.Validate(query);
            if (!validationResult.IsValid)
            {
                throw new SemanticQueryValidationException(validationResult.Errors);
            }

            ApplySemanticQuery(query);
            _queryDrawingToken = conversationContext.DrawingToken;
            _pendingTrialId = trialId;

            string scopeText = query.ScopeEntityIds.Count == 0
                ? "whole drawing"
                : query.ScopeSource == QueryScopeSource.PreviousResults
                    ? $"{query.ScopeEntityIds.Count} previous result(s)"
                    : $"{query.ScopeEntityIds.Count} selected entity(s)";
            string spatialText = query.SpatialFilter?.Relation.ToString() ?? "none";

            stopwatch.Stop();
            TryRecordEvaluation(
                trialId, EvaluationStage.Interpretation, true, stopwatch.ElapsedMilliseconds,
                query, null, EvaluationFailureKind.None);
            StatusText =
                $"Interpreted {query.EntityType} query with {query.Filters.Count} filter(s). " +
                $"Scope: {scopeText}. Spatial relation: {spatialText}. " +
                "Review it and press Execute query.";
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            TryRecordEvaluation(
                trialId, EvaluationStage.Interpretation, false, stopwatch.ElapsedMilliseconds,
                query, null, ClassifyFailure(exception));
            StatusText = $"Could not interpret query: {exception.Message}";
        }
        finally
        {
            IsInterpreting = false;
        }
    }

    private bool CanRecordFeedback()
    {
        return _evaluationRecorder.IsEnabled &&
               _lastResult is not null &&
               _lastExecutedTrialId != Guid.Empty &&
               _lastExecutionWasRecorded &&
               !_hasFeedbackForLastResult;
    }

    private void RefreshFeedbackCommands()
    {
        MarkResultExpectedCommand.RaiseCanExecuteChanged();
        MarkResultUnexpectedCommand.RaiseCanExecuteChanged();
    }

    private void RecordFeedback(bool matchesExpectation)
    {
        if (!CanRecordFeedback())
        {
            return;
        }

        bool recorded = TryRecordEvaluation(
            _lastExecutedTrialId, EvaluationStage.Feedback, true, 0,
            _lastResult!.Query, _lastResult.Count,
            EvaluationFailureKind.None, matchesExpectation);

        if (recorded)
        {
            _hasFeedbackForLastResult = true;
            RefreshFeedbackCommands();
            EvaluationStatusText = matchesExpectation
                ? "Evaluation: result marked as expected."
                : "Evaluation: result marked as unexpected.";
        }
    }

    private void ExportEvaluation()
    {
        try
        {
            string csvPath = _evaluationRecorder.ExportCsv();
            EvaluationStatusText = $"Evaluation CSV exported: {csvPath}";
        }
        catch (Exception exception)
        {
            EvaluationStatusText = $"Could not export evaluation: {exception.Message}";
        }
    }

    private bool TryRecordEvaluation(
        Guid trialId,
        EvaluationStage stage,
        bool success,
        long elapsedMilliseconds,
        SemanticQuery? query,
        int? resultCount,
        EvaluationFailureKind failureKind,
        bool? userConfirmedCorrect = null)
    {
        if (!_evaluationRecorder.IsEnabled)
        {
            return false;
        }

        try
        {
            _evaluationRecorder.Record(new EvaluationRecord
            {
                TrialId = trialId,
                Stage = stage,
                Success = success,
                ElapsedMilliseconds = elapsedMilliseconds,
                EntityType = query?.EntityType,
                FilterCount = query?.Filters.Count,
                ResultCount = resultCount,
                ScopeSource = query?.ScopeSource,
                SpatialRelation = query?.SpatialFilter?.Relation,
                FailureKind = failureKind,
                UserConfirmedCorrect = userConfirmedCorrect
            });
            return true;
        }
        catch (Exception exception)
        {
            // Telemetry errors must never break AutoCAD query execution.
            EvaluationStatusText = $"Metrics recording error: {exception.GetType().Name}";
            return false;
        }
    }

    private static EvaluationFailureKind ClassifyFailure(Exception exception)
    {
        return exception switch
        {
            SemanticQueryValidationException => EvaluationFailureKind.Validation,
            FormatException => EvaluationFailureKind.Input,
            ArgumentException => EvaluationFailureKind.Input,
            InvalidOperationException => EvaluationFailureKind.ContextOrConfiguration,
            _ => EvaluationFailureKind.Unknown
        };
    }

    private void ApplySemanticQuery(
        SemanticQuery query)
    {
        SelectedEntityType =
            query.EntityType;

        _scopeEntityIds = query.ScopeEntityIds.ToArray();
        _scopeSource = query.ScopeSource;
        _queryIntent = query.Intent;
        _spatialFilter = query.SpatialFilter;

        OnPropertyChanged(nameof(SpatialContextText));
        OnPropertyChanged(
            nameof(ScopeEntityCount));

        OnPropertyChanged(
            nameof(ScopeText));

        Filters.Clear();

        QueryPropertyOption[] availableProperties =
            GetAvailableProperties()
                .ToArray();

        foreach (
            QueryFilter queryFilter
            in query.Filters)
        {
            QueryFilterRowViewModel filterRow =
                new();

            filterRow.SetProperties(
                availableProperties);

            filterRow.Apply(
                queryFilter);

            Filters.Add(
                filterRow);
        }
    }
}
