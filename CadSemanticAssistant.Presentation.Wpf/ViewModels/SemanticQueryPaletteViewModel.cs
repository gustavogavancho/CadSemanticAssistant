using System.Collections.ObjectModel;
using System.Globalization;
using CadSemanticAssistant.Application.Queries;
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

    private SemanticEntityType _selectedEntityType;

    private SemanticQueryResult? _lastResult;

    private string _statusText = "Ready.";

    public SemanticQueryPaletteViewModel(ISemanticQuerySession querySession, SemanticMappingConfiguration configuration)
    {
        _querySession = querySession;

        _configuration = configuration;

        EntityTypes = new ObservableCollection<SemanticEntityType>(configuration.EntityMappings.Select(mapping => mapping.EntityType).Distinct());

        Filters = [];

        Results = [];

        AddFilterCommand = new RelayCommand(_ => AddFilter());

        RemoveFilterCommand = new RelayCommand(RemoveFilter);

        ExecuteCommand = new RelayCommand(_ => ExecuteQuery());

        SelectResultsCommand = new RelayCommand(_ => SelectResults(), _ => _lastResult?.Count > 0);

        ClearSelectionCommand = new RelayCommand(_ => ClearSelection());

        SelectedEntityType = EntityTypes.FirstOrDefault();

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

    public int ResultCount => Results.Count;

    public RelayCommand AddFilterCommand { get; }

    public RelayCommand RemoveFilterCommand { get; }

    public RelayCommand ExecuteCommand { get; }

    public RelayCommand SelectResultsCommand { get; }

    public RelayCommand ClearSelectionCommand { get; }

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
        try
        {
            SemanticQuery query = BuildQuery();

            SemanticQueryResult result = _querySession.Execute(query);

            _lastResult = result;

            Results.Clear();

            foreach (CadEntity entity in result.Entities)
            {
                Results.Add(new QueryResultItemViewModel(entity));
            }

            OnPropertyChanged(nameof(ResultCount));

            SelectResultsCommand.RaiseCanExecuteChanged();

            StatusText = $"Query executed successfully. {result.Count} result(s).";
        }
        catch (Exception exception)
        {
            _lastResult = null;

            Results.Clear();

            OnPropertyChanged(nameof(ResultCount));

            SelectResultsCommand.RaiseCanExecuteChanged();

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
            Intent = QueryIntent.Select,

            EntityType = SelectedEntityType,

            Filters = queryFilters
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
}
