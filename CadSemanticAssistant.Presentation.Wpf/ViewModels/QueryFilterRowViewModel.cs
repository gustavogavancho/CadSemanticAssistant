using System.Collections.ObjectModel;
using CadSemanticAssistant.Domain.Queries;

namespace CadSemanticAssistant.Presentation.Wpf.ViewModels;

public sealed class QueryFilterRowViewModel
    : ObservableObject
{
    private QueryPropertyOption? _selectedProperty;
    private QueryOperator? _selectedOperator;
    private string _value = string.Empty;

    public ObservableCollection<QueryPropertyOption> AvailableProperties { get; } = [];

    public ObservableCollection<QueryOperator> AvailableOperators { get; } = [];

    public QueryPropertyOption? SelectedProperty
    {
        get => _selectedProperty;

        set
        {
            if (!SetProperty(ref _selectedProperty, value))
            {
                return;
            }

            RefreshOperators();
        }
    }

    public QueryOperator? SelectedOperator
    {
        get => _selectedOperator;

        set => SetProperty(ref _selectedOperator, value);
    }

    public string Value
    {
        get => _value;

        set => SetProperty(ref _value, value);
    }

    public void SetProperties(IEnumerable<QueryPropertyOption> properties)
    {
        AvailableProperties.Clear();

        foreach (QueryPropertyOption property in properties)
        {
            AvailableProperties.Add(property);
        }

        SelectedProperty = AvailableProperties.FirstOrDefault();
    }

    private void RefreshOperators()
    {
        AvailableOperators.Clear();

        if (SelectedProperty is null)
        {
            SelectedOperator = null;
            return;
        }

        IEnumerable<QueryOperator> operators = SelectedProperty.DataType switch
        {
            Application.Semantics.SemanticDataType.String =>
            [
                QueryOperator.Equals,
                QueryOperator.NotEquals,
                QueryOperator.Contains
            ],

            Application.Semantics.SemanticDataType.Boolean =>
            [
                QueryOperator.Equals,
                QueryOperator.NotEquals
            ],

            _ =>
            [
                QueryOperator.Equals,
                QueryOperator.NotEquals,
                QueryOperator.GreaterThan,
                QueryOperator.GreaterThanOrEqual,
                QueryOperator.LessThan,
                QueryOperator.LessThanOrEqual
            ]
        };

        foreach (QueryOperator queryOperator in operators)
        {
            AvailableOperators.Add(queryOperator);
        }

        SelectedOperator = AvailableOperators.FirstOrDefault();
    }
}
