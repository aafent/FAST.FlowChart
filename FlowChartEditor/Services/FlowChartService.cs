using System.Text.Json;
using System.Text.Json.Serialization;
using FlowChartEditor.Commands;
using FlowChartEditor.Models;
using FlowChartEditor.Models.Artifacts;
using FlowChartEditor.Models.Ufa;

namespace FlowChartEditor.Services;

public class FlowChartService
{
    public FlowChart     Chart   { get; private set; } = new();
    public CommandHistory History { get; } = new();
    public bool HasUnsavedChanges { get; set; }

    private readonly UfaLoader _ufaLoader;

    public event Action? StateChanged;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters    = { new JsonStringEnumConverter() }
    };

    public FlowChartService(UfaLoader ufaLoader)
    {
        _ufaLoader = ufaLoader;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void New()
    {
        Chart = new FlowChart();
        History.Clear();
        HasUnsavedChanges = false;
        NotifyStateChanged();
    }

    public void LoadFromJson(string json)
    {
        var chart = JsonSerializer.Deserialize<FlowChart>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid chart JSON.");

        // Re-link UserDefinedArtifact instances to their UFA definitions
        // _baseInstance and Definition are [JsonIgnore] so must be restored after load
        foreach (var artifact in chart.Artifacts.OfType<UserDefinedArtifact>())
        {
            var instance = _ufaLoader.CreateInstance(artifact.UfaName, artifact.X, artifact.Y);
            if (instance != null && artifact.Definition == null)
            {
                // Restore Definition and _baseInstance via Initialise
                var def = _ufaLoader.Definitions.GetValueOrDefault(artifact.UfaName);
                if (def != null)
                {
                    var baseInstance = ArtifactFactory.Create(
                        Enum.Parse<ArtifactType>(def.BaseType, true), 0, 0);
                    artifact.Initialise(def, baseInstance);
                }
            }
        }

        Chart = chart;
        History.Clear();
        HasUnsavedChanges = false;
        NotifyStateChanged();
    }

    public string SaveToJson()
    {
        Chart.ModifiedAt  = DateTime.UtcNow;
        HasUnsavedChanges = false;
        return JsonSerializer.Serialize(Chart, JsonOptions);
    }

    /// <summary>Serialises the chart without marking it as saved — used by Save As.</summary>
    public string SaveToJsonWithoutChangingTitle()
    {
        Chart.ModifiedAt = DateTime.UtcNow;
        HasUnsavedChanges = false;
        return JsonSerializer.Serialize(Chart, JsonOptions);
    }

    // ── Artifact operations ───────────────────────────────────────────────────

    public FlowArtifact AddArtifact(ArtifactType type, double x, double y)
    {
        var artifact = ArtifactFactory.Create(type, x, y);
        var cmd      = new AddArtifactCommand(Chart, artifact);
        History.Execute(cmd);
        MarkDirty();
        NotifyStateChanged();
        return artifact;
    }

    public void AddUfaArtifact(Models.Ufa.UserDefinedArtifact ufa)
    {
        var cmd = new AddArtifactCommand(Chart, ufa);
        History.Execute(cmd);
        MarkDirty();
        NotifyStateChanged();
    }

    public void DeleteSelected()
    {
        var selectedArtifacts   = Chart.Artifacts.Where(a => a.IsSelected).ToList();
        var selectedConnections = Chart.Connections.Where(c => c.IsSelected).ToList();

        var artifactIds = selectedArtifacts.Select(a => a.Id).ToHashSet();
        var orphaned    = Chart.Connections
            .Where(c => (c.SourceArtifactId.HasValue && artifactIds.Contains(c.SourceArtifactId.Value))
                     || (c.TargetArtifactId.HasValue && artifactIds.Contains(c.TargetArtifactId.Value)))
            .Except(selectedConnections)
            .ToList();

        var allConnections = selectedConnections.Concat(orphaned).Distinct().ToList();
        if (!selectedArtifacts.Any() && !allConnections.Any()) return;

        var cmd = new DeleteArtifactsCommand(Chart, selectedArtifacts, allConnections);
        History.Execute(cmd);
        MarkDirty();
        NotifyStateChanged();
    }

    public void MoveArtifacts(IEnumerable<(FlowArtifact Artifact, double OldX, double OldY, double NewX, double NewY)> moves)
    {
        var list = moves.ToList();
        if (!list.Any()) return;
        History.Execute(new MoveArtifactsCommand(list));
        MarkDirty();
        NotifyStateChanged();
    }

    public void AlignVerticalCenters()
    {
        var selected = Chart.Artifacts.Where(a => a.IsSelected).ToList();
        if (selected.Count < 2) return;
        History.Execute(new AlignVerticalCentersCommand(selected));
        MarkDirty();
        NotifyStateChanged();
    }

    public void AlignHorizontalCenters()
    {
        var selected = Chart.Artifacts.Where(a => a.IsSelected).ToList();
        if (selected.Count < 2) return;
        History.Execute(new AlignHorizontalCentersCommand(selected));
        MarkDirty();
        NotifyStateChanged();
    }

    public void EditLabel(FlowArtifact artifact, string newLabel)
    {
        if (artifact.Label == newLabel) return;
        History.Execute(new EditLabelCommand(artifact, artifact.Label, newLabel));
        MarkDirty();
        NotifyStateChanged();
    }

    public void EditProperties(FlowArtifact artifact, string newLabel, List<PropertyValue> newValues)
    {
        History.Execute(new EditPropertiesCommand(artifact, artifact.Label, newLabel,
            artifact.PropertyValues, newValues));
        MarkDirty();
        NotifyStateChanged();
    }

    // ── Connection operations ─────────────────────────────────────────────────

    public ConnectionArtifact AddConnection(Guid sourceId, string sourcePortId,
                                             Guid targetId, string targetPortId)
    {
        var conn = ArtifactFactory.CreateConnection();
        conn.SourceArtifactId = sourceId;
        conn.SourcePortId     = sourcePortId;
        conn.TargetArtifactId = targetId;
        conn.TargetPortId     = targetPortId;

        // Auto-label: if source is a Switch case port, use the case value as label
        var sourceArtifact = Chart.Artifacts.FirstOrDefault(a => a.Id == sourceId);
        if (sourceArtifact is SwitchArtifact sw)
        {
            if (sourcePortId == SwitchArtifact.ElsePortId)
                conn.ConnectionLabel = "else";
            else
            {
                var matchingCase = sw.Cases.FirstOrDefault(c =>
                    SwitchArtifact.CasePortId(c.Id) == sourcePortId);
                if (matchingCase != null)
                    conn.ConnectionLabel = matchingCase.Value;
            }
        }

        History.Execute(new AddConnectionCommand(Chart, conn));
        MarkDirty();
        NotifyStateChanged();
        return conn;
    }

    public void ReconnectConnection(ConnectionArtifact conn,
        Guid? newSourceId, string newSourcePort,
        Guid? newTargetId, string newTargetPort)
    {
        History.Execute(new ReconnectConnectionCommand(
            conn,
            conn.SourceArtifactId, conn.SourcePortId, newSourceId, newSourcePort,
            conn.TargetArtifactId, conn.TargetPortId, newTargetId, newTargetPort));
        MarkDirty();
        NotifyStateChanged();
    }

    public void EditConnectionProperties(ConnectionArtifact conn,
        LineType lt, ArrowTerminator start, ArrowTerminator end,
        string label, LoopSide loopSide, string loopLabel, string lineColor)
    {
        History.Execute(new EditConnectionPropertiesCommand(
            conn, conn.LineType, lt,
            conn.StartTerminator, start,
            conn.EndTerminator, end,
            conn.ConnectionLabel, label,
            conn.LoopSide, loopSide,
            conn.LoopLabel, loopLabel,
            conn.LineColor, lineColor));
        MarkDirty();
        NotifyStateChanged();
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    public void SelectAll()
    {
        Chart.Artifacts.ForEach(a => a.IsSelected = true);
        Chart.Connections.ForEach(c => c.IsSelected = true);
        NotifyStateChanged();
    }

    public void ClearSelection()
    {
        Chart.Artifacts.ForEach(a => a.IsSelected = false);
        Chart.Connections.ForEach(c => c.IsSelected = false);
        NotifyStateChanged();
    }

    public void SelectArtifact(Guid id, bool addToSelection = false)
    {
        if (!addToSelection) ClearSelection();
        var a = Chart.FindArtifact(id);   if (a != null) a.IsSelected = true;
        var c = Chart.FindConnection(id); if (c != null) c.IsSelected = true;
        NotifyStateChanged();
    }

    public void SelectInRect(RectF rect)
    {
        Chart.Artifacts.ForEach(a   => a.IsSelected = rect.IntersectsWith(a.Bounds));
        Chart.Connections.ForEach(c => c.IsSelected = false);
        NotifyStateChanged();
    }

    // ── Undo / Redo ───────────────────────────────────────────────────────────

    public void Undo() { History.Undo(); MarkDirty(); NotifyStateChanged(); }
    public void Redo() { History.Redo(); MarkDirty(); NotifyStateChanged(); }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void MarkDirty()          => HasUnsavedChanges = true;
    public  void NotifyStateChanged() => StateChanged?.Invoke();
}
