using System.Text.Json;
using CadSemanticAssistant.Application.Catalog;
using CadSemanticAssistant.Application.Catalog.Validation;
using CadSemanticAssistant.Domain.Catalog;

namespace CadSemanticAssistant.Infrastructure.Configuration.Catalog;

public sealed class JsonBlockCatalogProvider : IBlockCatalogProvider
{
    private readonly string _filePath;
    private BlockCatalog? _cachedCatalog;

    public JsonBlockCatalogProvider(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Catalog path must be provided.", nameof(filePath));
        }

        _filePath = filePath;
    }

    public BlockCatalog GetCatalog()
    {
        if (_cachedCatalog is not null)
        {
            return _cachedCatalog;
        }

        if (!File.Exists(_filePath))
        {
            throw new FileNotFoundException("Block catalog file was not found.", _filePath);
        }

        string json = File.ReadAllText(_filePath);
        BlockCatalog? catalog;
        try
        {
            catalog = JsonSerializer.Deserialize<BlockCatalog>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid block catalog JSON at '{_filePath}'.", exception);
        }

        BlockCatalogDefinitionValidator validator = new();
        System.Collections.Generic.IReadOnlyList<string> errors = validator.Validate(catalog);
        if (errors.Count != 0)
        {
            throw new InvalidDataException(
                $"Invalid block catalog '{_filePath}':{Environment.NewLine} - "
                + string.Join(Environment.NewLine + " - ", errors));
        }

        _cachedCatalog = catalog!;
        return _cachedCatalog;
    }
}
