namespace FlowChartEditor.Models;

/// <summary>
/// A factor (column) in a Decision Table.
/// The last factor in the list is always the Result column.
/// </summary>
public class DtFactor
{
    public Guid   Id   { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public DtFactor Clone() => new() { Id = Id, Name = Name };
}

/// <summary>
/// A single rule row in a Decision Table.
/// Values are stored in the same order as the Factors list.
/// Each value can be: exact ("0"), range ("23:45"), comparison (">=3", "&lt;22"), wildcard ("*").
/// The last value corresponds to the Result factor.
/// </summary>
public class DtRow
{
    public Guid         Id     { get; set; } = Guid.NewGuid();
    public List<string> Values { get; set; } = new();

    public DtRow Clone() => new()
    {
        Id     = Id,
        Values = Values.Select(v => v).ToList()
    };
}
