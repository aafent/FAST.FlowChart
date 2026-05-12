using FlowChartEditor.Models;
using FlowChartEditor.Models.Artifacts;

namespace FlowChartEditor.Commands;

// ── Add Artifact ────────────────────────────────────────────────────────────
public class AddArtifactCommand : IFlowCommand
{
    private readonly FlowChart _chart;
    private readonly FlowArtifact _artifact;

    public string Description => $"Add {_artifact.Type}";

    public AddArtifactCommand(FlowChart chart, FlowArtifact artifact)
    {
        _chart = chart;
        _artifact = artifact;
    }

    public void Execute() => _chart.Artifacts.Add(_artifact);

    public void Undo() => _chart.Artifacts.Remove(_artifact);
}

// ── Delete Artifacts ─────────────────────────────────────────────────────────
public class DeleteArtifactsCommand : IFlowCommand
{
    private readonly FlowChart _chart;
    private readonly List<FlowArtifact> _artifacts;
    private readonly List<ConnectionArtifact> _connections;

    public string Description => $"Delete {_artifacts.Count + _connections.Count} item(s)";

    public DeleteArtifactsCommand(FlowChart chart, IEnumerable<FlowArtifact> artifacts, IEnumerable<ConnectionArtifact> connections)
    {
        _chart = chart;
        _artifacts = artifacts.ToList();
        _connections = connections.ToList();
    }

    public void Execute()
    {
        foreach (var a in _artifacts) _chart.Artifacts.Remove(a);
        foreach (var c in _connections) _chart.Connections.Remove(c);
    }

    public void Undo()
    {
        foreach (var a in _artifacts) _chart.Artifacts.Add(a);
        foreach (var c in _connections) _chart.Connections.Add(c);
    }
}

// ── Move Artifacts ────────────────────────────────────────────────────────────
public class MoveArtifactsCommand : IFlowCommand
{
    private readonly List<(FlowArtifact Artifact, double OldX, double OldY, double NewX, double NewY)> _moves;

    public string Description => "Move";

    public MoveArtifactsCommand(IEnumerable<(FlowArtifact, double, double, double, double)> moves)
    {
        _moves = moves.ToList();
    }

    public void Execute()
    {
        foreach (var (a, _, _, nx, ny) in _moves) { a.X = nx; a.Y = ny; }
    }

    public void Undo()
    {
        foreach (var (a, ox, oy, _, _) in _moves) { a.X = ox; a.Y = oy; }
    }
}

// ── Reconnect Connection ──────────────────────────────────────────────────────
public class ReconnectConnectionCommand : IFlowCommand
{
    private readonly ConnectionArtifact _connection;
    private readonly Guid?  _oldSourceId,  _newSourceId;
    private readonly string _oldSourcePort, _newSourcePort;
    private readonly Guid?  _oldTargetId,  _newTargetId;
    private readonly string _oldTargetPort, _newTargetPort;

    public string Description => "Reconnect Connection";

    public ReconnectConnectionCommand(ConnectionArtifact conn,
        Guid? oldSourceId, string oldSourcePort, Guid? newSourceId, string newSourcePort,
        Guid? oldTargetId, string oldTargetPort, Guid? newTargetId, string newTargetPort)
    {
        _connection    = conn;
        _oldSourceId   = oldSourceId;   _newSourceId   = newSourceId;
        _oldSourcePort = oldSourcePort; _newSourcePort = newSourcePort;
        _oldTargetId   = oldTargetId;   _newTargetId   = newTargetId;
        _oldTargetPort = oldTargetPort; _newTargetPort = newTargetPort;
    }

    public void Execute()
    {
        _connection.SourceArtifactId = _newSourceId;
        _connection.SourcePortId     = _newSourcePort;
        _connection.TargetArtifactId = _newTargetId;
        _connection.TargetPortId     = _newTargetPort;
    }

    public void Undo()
    {
        _connection.SourceArtifactId = _oldSourceId;
        _connection.SourcePortId     = _oldSourcePort;
        _connection.TargetArtifactId = _oldTargetId;
        _connection.TargetPortId     = _oldTargetPort;
    }
}
public class AlignVerticalCentersCommand : IFlowCommand
{
    private readonly List<(FlowArtifact Artifact, double OldY, double NewY)> _moves;

    public string Description => "Align Vertical Centers";

    public AlignVerticalCentersCommand(IEnumerable<FlowArtifact> artifacts)
    {
        // Target Y = average of all vertical centers (Y + Height/2)
        var list   = artifacts.ToList();
        var avgCY  = list.Average(a => a.Y + a.Height / 2);
        _moves     = list.Select(a => (a, a.Y, avgCY - a.Height / 2)).ToList();
    }

    public void Execute() { foreach (var (a, _, ny) in _moves) a.Y = ny; }
    public void Undo()    { foreach (var (a, oy, _) in _moves) a.Y = oy; }
}

// ── Align Horizontal Centers (same X center — vertical column) ────────────────
public class AlignHorizontalCentersCommand : IFlowCommand
{
    private readonly List<(FlowArtifact Artifact, double OldX, double NewX)> _moves;

    public string Description => "Align Horizontal Centers";

    public AlignHorizontalCentersCommand(IEnumerable<FlowArtifact> artifacts)
    {
        // Target X = average of all horizontal centers (X + Width/2)
        var list   = artifacts.ToList();
        var avgCX  = list.Average(a => a.X + a.Width / 2);
        _moves     = list.Select(a => (a, a.X, avgCX - a.Width / 2)).ToList();
    }

    public void Execute() { foreach (var (a, _, nx) in _moves) a.X = nx; }
    public void Undo()    { foreach (var (a, ox, _) in _moves) a.X = ox; }
}

// ── Add Connection ────────────────────────────────────────────────────────────
public class AddConnectionCommand : IFlowCommand
{
    private readonly FlowChart _chart;
    private readonly ConnectionArtifact _connection;

    public string Description => "Add Connection";

    public AddConnectionCommand(FlowChart chart, ConnectionArtifact connection)
    {
        _chart = chart;
        _connection = connection;
    }

    public void Execute() => _chart.Connections.Add(_connection);
    public void Undo() => _chart.Connections.Remove(_connection);
}

// ── Delete Connection ─────────────────────────────────────────────────────────
public class DeleteConnectionCommand : IFlowCommand
{
    private readonly FlowChart _chart;
    private readonly ConnectionArtifact _connection;

    public string Description => "Delete Connection";

    public DeleteConnectionCommand(FlowChart chart, ConnectionArtifact connection)
    {
        _chart = chart;
        _connection = connection;
    }

    public void Execute() => _chart.Connections.Remove(_connection);
    public void Undo() => _chart.Connections.Add(_connection);
}

// ── Edit Label ────────────────────────────────────────────────────────────────
public class EditLabelCommand : IFlowCommand
{
    private readonly FlowArtifact _artifact;
    private readonly string _oldLabel;
    private readonly string _newLabel;

    public string Description => "Edit Label";

    public EditLabelCommand(FlowArtifact artifact, string oldLabel, string newLabel)
    {
        _artifact = artifact;
        _oldLabel = oldLabel;
        _newLabel = newLabel;
    }

    public void Execute() => _artifact.Label = _newLabel;
    public void Undo() => _artifact.Label = _oldLabel;
}

// ── Edit Properties ───────────────────────────────────────────────────────────
public class EditPropertiesCommand : IFlowCommand
{
    private readonly FlowArtifact _artifact;
    private readonly List<PropertyValue> _oldValues;
    private readonly List<PropertyValue> _newValues;
    private readonly string _oldLabel;
    private readonly string _newLabel;

    public string Description => "Edit Properties";

    public EditPropertiesCommand(FlowArtifact artifact, string oldLabel, string newLabel,
        List<PropertyValue> oldValues, List<PropertyValue> newValues)
    {
        _artifact = artifact;
        _oldLabel = oldLabel;
        _newLabel = newLabel;
        _oldValues = oldValues.Select(v => new PropertyValue { Key = v.Key, Value = v.Value }).ToList();
        _newValues = newValues.Select(v => new PropertyValue { Key = v.Key, Value = v.Value }).ToList();
    }

    public void Execute()
    {
        _artifact.Label = _newLabel;
        _artifact.PropertyValues = _newValues.Select(v => new PropertyValue { Key = v.Key, Value = v.Value }).ToList();
    }

    public void Undo()
    {
        _artifact.Label = _oldLabel;
        _artifact.PropertyValues = _oldValues.Select(v => new PropertyValue { Key = v.Key, Value = v.Value }).ToList();
    }
}

// ── Edit Connection Properties ────────────────────────────────────────────────
public class EditConnectionPropertiesCommand : IFlowCommand
{
    private readonly ConnectionArtifact _connection;
    private readonly LineType        _oldLineType,  _newLineType;
    private readonly ArrowTerminator _oldStart,     _newStart;
    private readonly ArrowTerminator _oldEnd,       _newEnd;
    private readonly string          _oldLabel,     _newLabel;
    private readonly LoopSide        _oldLoopSide,  _newLoopSide;
    private readonly string          _oldLoopLabel, _newLoopLabel;
    private readonly string          _oldLineColor, _newLineColor;

    public string Description => "Edit Connection";

    public EditConnectionPropertiesCommand(ConnectionArtifact c,
        LineType oldLt,       LineType newLt,
        ArrowTerminator oldStart, ArrowTerminator newStart,
        ArrowTerminator oldEnd,   ArrowTerminator newEnd,
        string oldLabel,          string newLabel,
        LoopSide oldLoopSide,     LoopSide newLoopSide,
        string oldLoopLabel,      string newLoopLabel,
        string oldLineColor,      string newLineColor)
    {
        _connection    = c;
        _oldLineType   = oldLt;        _newLineType   = newLt;
        _oldStart      = oldStart;     _newStart      = newStart;
        _oldEnd        = oldEnd;       _newEnd        = newEnd;
        _oldLabel      = oldLabel;     _newLabel      = newLabel;
        _oldLoopSide   = oldLoopSide;  _newLoopSide   = newLoopSide;
        _oldLoopLabel  = oldLoopLabel; _newLoopLabel  = newLoopLabel;
        _oldLineColor  = oldLineColor; _newLineColor  = newLineColor;
    }

    public void Execute()
    {
        _connection.LineType        = _newLineType;
        _connection.StartTerminator = _newStart;
        _connection.EndTerminator   = _newEnd;
        _connection.ConnectionLabel = _newLabel;
        _connection.LoopSide        = _newLoopSide;
        _connection.LoopLabel       = _newLoopLabel;
        _connection.LineColor       = _newLineColor;
    }

    public void Undo()
    {
        _connection.LineType        = _oldLineType;
        _connection.StartTerminator = _oldStart;
        _connection.EndTerminator   = _oldEnd;
        _connection.ConnectionLabel = _oldLabel;
        _connection.LoopSide        = _oldLoopSide;
        _connection.LoopLabel       = _oldLoopLabel;
        _connection.LineColor       = _oldLineColor;
    }
}
