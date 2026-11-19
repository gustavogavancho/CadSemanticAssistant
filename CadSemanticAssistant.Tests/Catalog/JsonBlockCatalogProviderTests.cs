using System.Text.Json.Nodes;
using CadSemanticAssistant.Application.Catalog.Validation;
using CadSemanticAssistant.Domain.Catalog;
using CadSemanticAssistant.Infrastructure.Configuration.Catalog;

namespace CadSemanticAssistant.Tests.Catalog;

public sealed class JsonBlockCatalogProviderTests
{
    private static string CatalogPath => Path.Combine(
        AppContext.BaseDirectory, "TestData", "catalogo_bloques.v1.json");

    [Fact]
    public void GetCatalog_LoadsNineBlockDefinitions()
    {
        JsonBlockCatalogProvider provider = new(CatalogPath);

        BlockCatalog catalog = provider.GetCatalog();

        Assert.Equal("1.0.0", catalog.SchemaVersion);
        Assert.Equal(9, catalog.CatalogItems.Length);
        Assert.Equal(3, catalog.CatalogItems.Count(i => i.SemanticType == "Rack"));
        Assert.Equal(4, catalog.CatalogItems.Count(i => i.SemanticType == "Machine"));
        Assert.Equal(2, catalog.CatalogItems.Count(i => i.SemanticType == "Complement"));
        Assert.NotNull(catalog.FindByBlockName("rack_sc2_a"));
        Assert.Null(catalog.FindByBlockName("NOT_IN_CATALOG"));
    }

    [Fact]
    public void GetCatalog_UsesSameCachedCatalogInstance()
    {
        JsonBlockCatalogProvider provider = new(CatalogPath);
        Assert.Same(provider.GetCatalog(), provider.GetCatalog());
    }

    [Fact]
    public void GetCatalog_RejectsMissingFile()
    {
        JsonBlockCatalogProvider provider = new(Path.Combine(
            Path.GetTempPath(), $"missing-catalog-{Guid.NewGuid():N}.json"));
        Assert.Throws<FileNotFoundException>(provider.GetCatalog);
    }

    [Fact]
    public void GetCatalog_RejectsDuplicateBlockName()
    {
        AssertInvalidCatalog(root =>
        {
            JsonArray items = (JsonArray)root["catalogItems"]!;
            items[1]!["blockName"] = "RACK_SC1_A";
        }, "duplicate blockName");
    }

    [Fact]
    public void GetCatalog_RejectsUnknownParentModel()
    {
        AssertInvalidCatalog(root =>
        {
            JsonArray items = (JsonArray)root["catalogItems"]!;
            items[7]!["allowedParentModels"]![0] = "NOT_A_MACHINE";
        }, "unknown allowed parent model");
    }

    [Fact]
    public void GetCatalog_RejectsInvalidCircleRadius()
    {
        AssertInvalidCatalog(root =>
        {
            JsonArray items = (JsonArray)root["catalogItems"]!;
            items[3]!["footprint"]!["radiusMm"] = -1;
        }, "positive radiusMm");
    }

    [Fact]
    public void GetCatalog_RejectsInvalidAttributePattern()
    {
        AssertInvalidCatalog(root =>
        {
            JsonArray items = (JsonArray)root["catalogItems"]!;
            items[0]!["attributes"]![0]!["pattern"] = "[";
        }, "invalid regular expression");
    }

    [Fact]
    public void DefinitionValidator_AcceptsOriginalCatalog()
    {
        BlockCatalog catalog = new JsonBlockCatalogProvider(CatalogPath).GetCatalog();
        BlockCatalogDefinitionValidator validator = new();
        Assert.Empty(validator.Validate(catalog));
    }

    private static void AssertInvalidCatalog(Action<JsonNode> change, string expectedMessage)
    {
        JsonNode root = JsonNode.Parse(File.ReadAllText(CatalogPath))!;
        change(root);
        string tempPath = Path.Combine(Path.GetTempPath(), $"catalog-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(tempPath, root.ToJsonString());
            JsonBlockCatalogProvider provider = new(tempPath);
            InvalidDataException exception = Assert.Throws<InvalidDataException>(provider.GetCatalog);
            Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
