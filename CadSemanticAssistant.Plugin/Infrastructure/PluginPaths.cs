using System.IO;

namespace CadSemanticAssistant.Plugin.Infrastructure;

public static class PluginPaths
{
    public static string PluginDirectory
    {
        get
        {
            string assemblyLocation = typeof(PluginEntryPoint).Assembly.Location;

            return Path.GetDirectoryName(assemblyLocation)
                   ?? throw new InvalidOperationException("Plugin directory could not be resolved.");
        }
    }

    public static string SemanticMappingConfiguration => Path.Combine(PluginDirectory, "Config", "semantic-mapping.json");
}
