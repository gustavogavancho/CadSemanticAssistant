using Autodesk.AutoCAD.Runtime;

using CadSemanticAssistant.Plugin;
using CadSemanticAssistant.Plugin.Commands;

[assembly: ExtensionApplication(typeof(PluginEntryPoint))]
[assembly: CommandClass(typeof(CadCommands))]
