using CadSemanticAssistant.Application.Insertions.Validation;
using CadSemanticAssistant.Infrastructure.Configuration.Catalog;

namespace CadSemanticAssistant.Tests.Insertions;

public sealed class JsonSemanticRuleSettingsLoaderTests
{
    [Fact]
    public void Load_UsesExactSem005AndSem008Settings()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "TestData", "reglas_dominio.v1.json");
        SemanticValidationSettings rules = JsonSemanticRuleSettingsLoader.Load(path);
        Assert.Equal(1, rules.MinimumInsertions);
        Assert.Equal(6, rules.MaximumInsertions);
        Assert.Equal(1, rules.MaximumComplementsPerTypePerParent);
    }

    [Fact]
    public void Load_RejectsMissingRules()
    {
        string path = Path.Combine(Path.GetTempPath(), $"empty-rules-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path,
                "{\"schemaVersion\":\"1.0.0\",\"artifact\":\"domain-rules\",\"rules\":[]}");
            Assert.Throws<InvalidDataException>(() => JsonSemanticRuleSettingsLoader.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
