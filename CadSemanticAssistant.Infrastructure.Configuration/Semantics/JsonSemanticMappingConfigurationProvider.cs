using System.Text.Json;
using System.Text.Json.Serialization;
using CadSemanticAssistant.Application.Semantics.Configuration;

namespace CadSemanticAssistant.Infrastructure.Configuration.Semantics;

public sealed class JsonSemanticMappingConfigurationProvider
    : ISemanticMappingConfigurationProvider
{
    private readonly string _filePath;

    private SemanticMappingConfiguration? _cachedConfiguration;

    public JsonSemanticMappingConfigurationProvider(string filePath)
    {
        _filePath = filePath;
    }

    public SemanticMappingConfiguration GetConfiguration()
    {
        if (_cachedConfiguration is not null)
        {
            return _cachedConfiguration;
        }

        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException("Semantic mapping configuration file was not found.", _filePath);
        }

        string json = File.ReadAllText(_filePath);

        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        options.Converters.Add(new JsonStringEnumConverter());

        SemanticMappingConfiguration? configuration = JsonSerializer.Deserialize<SemanticMappingConfiguration>(json, options);

        if (configuration is null)
        {
            throw new InvalidOperationException("Semantic mapping configuration could not be deserialized.");
        }

        _cachedConfiguration = configuration;

        return configuration;
    }
}
