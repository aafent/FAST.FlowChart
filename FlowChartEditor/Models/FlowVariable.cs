using System.Text.Json.Serialization;

namespace FlowChartEditor.Models;

/// <summary>
/// Represents a diagram-level variable defined at the Start Terminal.
/// These act as global variables initialized when the flow begins.
/// </summary>
public class FlowVariable
{
    public Guid   Id           { get; set; } = Guid.NewGuid();
    public string Name         { get; set; } = string.Empty;
    public VariableType Type   { get; set; } = VariableType.Text;
    public string InitialValue { get; set; } = string.Empty;

    /// <summary>Runtime sort order — not persisted, used for drag reorder.</summary>
    [JsonIgnore] public int SortOrder { get; set; }

    public FlowVariable Clone() => new()
    {
        Id           = Id,
        Name         = Name,
        Type         = Type,
        InitialValue = InitialValue,
        SortOrder    = SortOrder
    };
}

public enum VariableType
{
    Text,
    Numeric,
    Date,
    Button,
    ListOfValues
}
