using System.Drawing;

using Autodesk.AutoCAD.Windows;

using CadSemanticAssistant.Application.Queries;
using CadSemanticAssistant.Application.Semantics.Configuration;

using CadSemanticAssistant.Infrastructure.Configuration.Semantics;

using CadSemanticAssistant.Plugin.Infrastructure;
using CadSemanticAssistant.Plugin.Services;

using CadSemanticAssistant.Presentation.Wpf.ViewModels;
using CadSemanticAssistant.Presentation.Wpf.Views;


namespace CadSemanticAssistant.Plugin.UI;

public static class SemanticAssistantPaletteHost
{
    private static readonly Guid PaletteId = new("A9CF5C71-4152-46A4-A0A4-5B40FB42C15B");

    private static PaletteSet? _paletteSet;

    public static void Show()
    {
        EnsureCreated();
        _paletteSet!.Visible = true;
    }

    private static void EnsureCreated()
    {
        if (_paletteSet is not null)
        {
            return;
        }

        string configurationPath = PluginPaths.SemanticMappingConfiguration;

        ISemanticMappingConfigurationProvider configurationProvider = new JsonSemanticMappingConfigurationProvider(configurationPath);

        SemanticMappingConfiguration configuration = configurationProvider.GetConfiguration();

        ISemanticQuerySession querySession = new AutoCadSemanticQuerySession(configurationPath);

        SemanticQueryPaletteViewModel viewModel = new(querySession, configuration);

        SemanticQueryPaletteView view = new()
        {
            DataContext = viewModel
        };

        PaletteSet paletteSet = new("CAD Semantic Assistant", PaletteId)
        {
            DockEnabled = DockSides.Left | DockSides.Right,

            MinimumSize = new Size(320, 450),

            Size = new Size(420, 700),

            Style = PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu
        };

        paletteSet.AddVisual("Query", view, true);

        _paletteSet = paletteSet;
    }
}
