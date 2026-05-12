using System.Text.Json.Serialization;
using FlowChartEditor.Models.Artifacts;
using FlowChartEditor.Models.Ufa;

namespace FlowChartEditor.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(StartTerminalArtifact),  "start")]
[JsonDerivedType(typeof(EndTerminalArtifact),    "end")]
[JsonDerivedType(typeof(ProcessArtifact),        "process")]
[JsonDerivedType(typeof(DecisionArtifact),       "decision")]
[JsonDerivedType(typeof(InputOutputArtifact),    "inputoutput")]
[JsonDerivedType(typeof(DatabaseArtifact),       "database")]
[JsonDerivedType(typeof(PreparationArtifact),    "preparation")]
[JsonDerivedType(typeof(DocumentArtifact),       "document")]
[JsonDerivedType(typeof(NoteArtifact),           "note")]
[JsonDerivedType(typeof(ConnectionArtifact),        "connection")]
[JsonDerivedType(typeof(OnPageConnectorArtifact),   "onpageconnector")]
[JsonDerivedType(typeof(PredefinedProcessArtifact), "predefinedprocess")]
[JsonDerivedType(typeof(Ufa.UserDefinedArtifact),   "userdefined")]
[JsonDerivedType(typeof(Artifacts.DecisionTableArtifact), "decisiontable")]
[JsonDerivedType(typeof(Artifacts.SwitchArtifact),       "switch")]
public abstract class FlowArtifact
{
    public Guid         Id     { get; set; } = Guid.NewGuid();
    public ArtifactType Type   { get; protected set; }
    public double X            { get; set; }
    public double Y            { get; set; }
    public double Width        { get; set; }
    public double Height       { get; set; }
    public string Label        { get; set; } = string.Empty;

    // ── Stencil metadata (not serialised, not user-editable) ─────────────────
    /// <summary>The stencil group this artifact belongs to. Default: "SHAPES".</summary>
    [JsonIgnore] public string StencilGroup    { get; protected set; } = "SHAPES";

    /// <summary>The tab within the stencil group. e.g. "Standard", "Data", "Decoration".</summary>
    [JsonIgnore] public string StencilTab { get; protected set; } = "Standard";

    // ── Loop participation flags ───────────────────────────────────────────────
    /// <summary>This artifact can be the SOURCE of a loop connection line.</summary>
    public bool CanBeLoopBegin { get; set; }

    /// <summary>This artifact can be the TARGET of a loop connection line.</summary>
    public bool CanBeLoopEnd   { get; set; }

    // ── Runtime UI state (not serialised) ─────────────────────────────────────
    [JsonIgnore] public bool IsSelected     { get; set; }
    [JsonIgnore] public bool IsEditingLabel { get; set; }

    public List<PropertyValue> PropertyValues { get; set; } = new();

    [JsonIgnore] public RectF  Bounds => new(X, Y, Width, Height);
    [JsonIgnore] public PointF Center => new(X + Width / 2, Y + Height / 2);

    [JsonIgnore]
    public virtual List<ConnectionPort> Ports
    {
        get
        {
            var ports = new List<ConnectionPort>();
            var cx = X + Width  / 2;
            var cy = Y + Height / 2;
            foreach (var pd in GetPortDescriptors())
            {
                ports.Add(new ConnectionPort
                {
                    Id        = pd.Id,
                    Direction = pd.Direction,
                    Position  = pd.Side switch
                    {
                        PortSide.Top         => new PointF(cx, Y),
                        PortSide.Bottom      => new PointF(cx, Y + Height),
                        PortSide.Left        => new PointF(X,  cy),
                        PortSide.Right       => new PointF(X + Width, cy),
                        PortSide.TopLeft     => new PointF(X + Width * 0.25, Y),
                        PortSide.TopRight    => new PointF(X + Width * 0.75, Y),
                        PortSide.BottomLeft  => new PointF(X + Width * 0.25, Y + Height),
                        PortSide.BottomRight => new PointF(X + Width * 0.75, Y + Height),
                        _ => Center
                    }
                });
            }
            return ports;
        }
    }

    // ── Abstract contract ─────────────────────────────────────────────────────
    public abstract string                        RenderSvgShape();
    public abstract string                        RenderStencilShape();
    public abstract List<PortDescriptor>          GetPortDescriptors();
    public abstract List<PropertyFieldDefinition> GetPropertyDefinitions();

    // ── Virtual hooks ─────────────────────────────────────────────────────────
    public virtual void OnPropertyChanged(string key, object? newValue) { }
    public virtual void OnAddedToCanvas() { }

    // ── Property value helpers ────────────────────────────────────────────────
    public void SetValue(string key, object? value)
    {
        var pv = PropertyValues.FirstOrDefault(v => v.Key == key);
        if (pv != null) { pv.Value = value; OnPropertyChanged(key, value); }
        else PropertyValues.Add(new PropertyValue { Key = key, Value = value });
    }

    public object? GetValue(string key) =>
        PropertyValues.FirstOrDefault(v => v.Key == key)?.Value;

    public string GetString(string key, string fallback = "") =>
        PropertyValues.FirstOrDefault(v => v.Key == key)?.AsString() ?? fallback;

    public void InitialiseDefaults()
    {
        foreach (var def in GetPropertyDefinitions())
            if (PropertyValues.All(v => v.Key != def.Key))
                PropertyValues.Add(new PropertyValue { Key = def.Key, Value = def.DefaultValue });
        if (string.IsNullOrEmpty(Label))
            Label = GetString("label", Type.ToString());
    }

    /// <summary>
    /// Base properties always available on every artifact regardless of type.
    /// These appear at the bottom of the Properties modal.
    /// </summary>
    public List<PropertyFieldDefinition> GetBasePropertyDefinitions() => new()
    {
        new() { Key = "notes",     Label = "Notes",      FieldType = PropertyFieldType.Text, DefaultValue = "" },
        new() { Key = "tag",       Label = "Tag / Group",FieldType = PropertyFieldType.Text, DefaultValue = "" }
    };
}

public class ConnectionPort
{
    public string        Id        { get; set; } = string.Empty;
    public PortDirection Direction { get; set; }
    public PointF        Position  { get; set; }
}

public enum PortDirection { Input, Output }
