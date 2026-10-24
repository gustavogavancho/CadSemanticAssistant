using CadSemanticAssistant.Domain.Models;

namespace CadSemanticAssistant.Presentation.Wpf.ViewModels;

public sealed class QueryResultItemViewModel
{
    public QueryResultItemViewModel(CadEntity entity)
    {
        Id = entity.Id;
        Name = entity.Name;
        Type = entity.Type.ToString();
        Layer = entity.Layer;

        Properties =
            string.Join(
                ", ",
                entity.Properties.Select(
                    property =>
                    {
                        string unit =
                            string.IsNullOrWhiteSpace(
                                property.Value.Unit)
                                ? string.Empty
                                : $" {property.Value.Unit}";

                        return
                            $"{property.Key}=" +
                            $"{property.Value.Value}{unit}";
                    }));
    }

    public string Id { get; }
    public string Name { get; }
    public string Type { get; }
    public string Layer { get; }
    public string Properties { get; }
}
