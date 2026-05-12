namespace FlowChartEditor.Models;

/// <summary>
/// Represents an output mapping on an EndTerminal.
/// Name is the output identifier; Value maps to a StartTerminal
/// Argument (blue), Variable (black), or free text.
/// </summary>
public class FlowOutput
{
    public Guid   Id    { get; set; } = Guid.NewGuid();
    public string Name  { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public FlowOutput Clone() => new() { Id = Id, Name = Name, Value = Value };
}
