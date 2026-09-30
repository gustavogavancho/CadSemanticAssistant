using CadSemanticAssistant.Application.Semantics.Configuration;
using CadSemanticAssistant.Domain.Semantics;
using CadSemanticAssistant.Infrastructure.Configuration.Semantics;

namespace CadSemanticAssistant.Tests.Configuration;

public sealed class JsonSemanticMappingConfigurationProviderTests
{
    [Fact]
    public void GetConfiguration_LoadsSemanticMappingsFromJsonFile()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"semantic-mapping-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(
                filePath,
                """
                {
                  "entityMappings": [
                    {
                      "entityType": "Rack",
                      "blockNamePatterns": ["RACK_*"],
                      "properties": []
                    },
                    {
                      "entityType": "Machine",
                      "blockNamePatterns": ["MACHINE_*"],
                      "properties": []
                    }
                  ]
                }
                """);

            JsonSemanticMappingConfigurationProvider provider =
                new(filePath);

            SemanticMappingConfiguration configuration =
                provider.GetConfiguration();

            Assert.Equal(2, configuration.EntityMappings.Length);
            Assert.Equal(
                SemanticEntityType.Rack,
                configuration.EntityMappings[0].EntityType);
            Assert.Equal(
                "RACK_*",
                configuration.EntityMappings[0].BlockNamePatterns[0]);
            Assert.Equal(
                SemanticEntityType.Machine,
                configuration.EntityMappings[1].EntityType);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void GetConfiguration_ThrowsWhenJsonFileDoesNotExist()
    {
        string filePath = Path.Combine(
            Path.GetTempPath(),
            $"missing-semantic-mapping-{Guid.NewGuid():N}.json");

        JsonSemanticMappingConfigurationProvider provider =
            new(filePath);

        Assert.Throws<FileNotFoundException>(provider.GetConfiguration);
    }
}
