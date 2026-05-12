using FlowChartEditor.Models.Artifacts;

namespace FlowChartEditor.Models;

/// <summary>
/// Creates concrete artifact instances by type.
/// Stencil grouping is fully data-driven from each artifact's
/// Stencil and StencilTab properties — no hardcoded categories.
/// </summary>
public static class ArtifactFactory
{
    public static FlowArtifact Create(ArtifactType type, double x = 0, double y = 0)
    {
        FlowArtifact artifact = type switch
        {
            ArtifactType.StartTerminal     => new StartTerminalArtifact(),
            ArtifactType.EndTerminal       => new EndTerminalArtifact(),
            ArtifactType.Process           => new ProcessArtifact(),
            ArtifactType.Decision          => new DecisionArtifact(),
            ArtifactType.InputOutput       => new InputOutputArtifact(),
            ArtifactType.Database          => new DatabaseArtifact(),
            ArtifactType.Preparation       => new PreparationArtifact(),
            ArtifactType.Document          => new DocumentArtifact(),
            ArtifactType.Note              => new NoteArtifact(),
            ArtifactType.Connection        => new ConnectionArtifact(),
            ArtifactType.OnPageConnector   => new OnPageConnectorArtifact(),
            ArtifactType.PredefinedProcess => new PredefinedProcessArtifact(),
            ArtifactType.DecisionTable     => new DecisionTableArtifact(),
            ArtifactType.Switch            => new SwitchArtifact(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        artifact.X = x;
        artifact.Y = y;
        artifact.InitialiseDefaults();
        artifact.OnAddedToCanvas();
        return artifact;
    }

    public static ConnectionArtifact CreateConnection() =>
        (ConnectionArtifact)Create(ArtifactType.Connection);

    // ── Stencil info ──────────────────────────────────────────────────────────

    public static StencilInfo GetStencilInfo(ArtifactType type)
    {
        var a = Create(type, 0, 0);
        return new StencilInfo(
            type,
            GetDisplayName(type),
            a.StencilGroup,
            a.StencilTab,
            GetFillColor(type),
            GetStrokeColor(type),
            GetTextColor(type));
    }

    /// <summary>
    /// Returns all stencil groups discovered from artifact definitions.
    /// Each group contains its tabs and items — fully data-driven.
    /// </summary>
    public static List<StencilGroupInfo> GetStencilGroups()
    {
        var allTypes = Enum.GetValues<ArtifactType>()
            .Where(t => t != ArtifactType.Connection && t != ArtifactType.UserDefined)
            .Select(GetStencilInfo)
            .ToList();

        return allTypes
            .GroupBy(i => i.StencilGroup)
            .Select(g => new StencilGroupInfo(
                g.Key,
                g.GroupBy(i => i.StencilTab)
                  .Select(t => new StencilTabInfo(
                      t.Key,
                      t.OrderBy(i => i.DisplayName).ToList()))
                  .OrderBy(t => t.Name switch
                  {
                      "Chart"  => 0,
                      "Logic"  => 1,
                      "Data"   => 2,
                      _        => 99
                  }).ToList()))
            .ToList();
    }

    /// <summary>Returns items for a specific stencil group and tab.</summary>
    public static IEnumerable<StencilInfo> GetStencilItems(string stencil, string tab) =>
        Enum.GetValues<ArtifactType>()
            .Where(t => t != ArtifactType.Connection && t != ArtifactType.UserDefined)
            .Select(GetStencilInfo)
            .Where(i => i.StencilGroup == stencil && i.StencilTab == tab)
            .OrderBy(i => i.DisplayName);

    // ── Display metadata (kept here to avoid creating instances for just colors) ──

    public static string GetDisplayName(ArtifactType type) => type switch
    {
        ArtifactType.StartTerminal     => "Start",
        ArtifactType.EndTerminal       => "End",
        ArtifactType.Process           => "Process",
        ArtifactType.Decision          => "Decision",
        ArtifactType.InputOutput       => "Input / Output",
        ArtifactType.Database          => "Database",
        ArtifactType.Preparation       => "Preparation",
        ArtifactType.Document          => "Document",
        ArtifactType.Note              => "Note",
        ArtifactType.OnPageConnector   => "On-Page Connector",
        ArtifactType.PredefinedProcess => "Predefined Process",
        ArtifactType.DecisionTable     => "Decision Table",
        ArtifactType.Switch            => "Switch",
        _ => type.ToString()
    };

    private static string GetFillColor(ArtifactType type) => type switch
    {
        ArtifactType.StartTerminal     => StartTerminalArtifact.FillColor,
        ArtifactType.EndTerminal       => EndTerminalArtifact.FillColor,
        ArtifactType.Process           => ProcessArtifact.FillColor,
        ArtifactType.Decision          => DecisionArtifact.FillColor,
        ArtifactType.InputOutput       => InputOutputArtifact.FillColor,
        ArtifactType.Database          => DatabaseArtifact.FillColor,
        ArtifactType.Preparation       => PreparationArtifact.FillColor,
        ArtifactType.Document          => DocumentArtifact.FillColor,
        ArtifactType.Note              => NoteArtifact.FillColor,
        ArtifactType.OnPageConnector   => OnPageConnectorArtifact.FillColor,
        ArtifactType.PredefinedProcess => PredefinedProcessArtifact.FillColor,
        ArtifactType.DecisionTable     => DecisionTableArtifact.FillColor,
        ArtifactType.Switch            => SwitchArtifact.FillColor,
        _ => "#e5e7eb"
    };

    private static string GetStrokeColor(ArtifactType type) => type switch
    {
        ArtifactType.StartTerminal     => StartTerminalArtifact.StrokeColor,
        ArtifactType.EndTerminal       => EndTerminalArtifact.StrokeColor,
        ArtifactType.Process           => ProcessArtifact.StrokeColor,
        ArtifactType.Decision          => DecisionArtifact.StrokeColor,
        ArtifactType.InputOutput       => InputOutputArtifact.StrokeColor,
        ArtifactType.Database          => DatabaseArtifact.StrokeColor,
        ArtifactType.Preparation       => PreparationArtifact.StrokeColor,
        ArtifactType.Document          => DocumentArtifact.StrokeColor,
        ArtifactType.Note              => NoteArtifact.StrokeColor,
        ArtifactType.OnPageConnector   => OnPageConnectorArtifact.StrokeColor,
        ArtifactType.PredefinedProcess => PredefinedProcessArtifact.StrokeColor,
        ArtifactType.DecisionTable     => DecisionTableArtifact.StrokeColor,
        ArtifactType.Switch            => SwitchArtifact.StrokeColor,
        _ => "#9ca3af"
    };

    private static string GetTextColor(ArtifactType type) => type switch
    {
        ArtifactType.StartTerminal     => StartTerminalArtifact.TextColor,
        ArtifactType.EndTerminal       => EndTerminalArtifact.TextColor,
        ArtifactType.Process           => ProcessArtifact.TextColor,
        ArtifactType.Decision          => DecisionArtifact.TextColor,
        ArtifactType.InputOutput       => InputOutputArtifact.TextColor,
        ArtifactType.Database          => DatabaseArtifact.TextColor,
        ArtifactType.Preparation       => PreparationArtifact.TextColor,
        ArtifactType.Document          => DocumentArtifact.TextColor,
        ArtifactType.Note              => NoteArtifact.TextColor,
        ArtifactType.OnPageConnector   => OnPageConnectorArtifact.TextColor,
        ArtifactType.PredefinedProcess => PredefinedProcessArtifact.TextColor,
        ArtifactType.DecisionTable     => DecisionTableArtifact.TextColor,
        ArtifactType.Switch            => SwitchArtifact.TextColor,
        _ => "#1f2937"
    };
}

// ── Data transfer records ─────────────────────────────────────────────────────

public record StencilInfo(
    ArtifactType Type,
    string       DisplayName,
    string       StencilGroup,
    string       StencilTab,
    string       FillColor,
    string       StrokeColor,
    string       TextColor);

public record StencilGroupInfo(
    string               Name,
    List<StencilTabInfo> Tabs);

public record StencilTabInfo(
    string           Name,
    List<StencilInfo> Items);
