using Autodesk.AutoCAD.Runtime;

using CadSemanticAssistant.Plugin;
using CadSemanticAssistant.Plugin.Commands;

[assembly: ExtensionApplication(typeof(PluginEntryPoint))]
[assembly: CommandClass(typeof(CadCommands))]
[assembly: CommandClass(typeof(BlockCatalogCommands))]
[assembly: CommandClass(typeof(TestDataCommands))]
