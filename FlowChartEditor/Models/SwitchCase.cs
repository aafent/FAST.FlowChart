namespace FlowChartEditor.Models;

/// <summary>
/// A single case (row) in a Switch artifact.
/// Each case has a value to match and generates one output port on the right side.
/// </summary>
public class SwitchCase
{
    public Guid   Id    { get; set; } = Guid.NewGuid();
    public string Value { get; set; } = string.Empty;

    public SwitchCase Clone() => new() { Id = Id, Value = Value };
}
