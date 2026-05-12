namespace FlowChartEditor.Models;

public class PropertyFieldDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public PropertyFieldType FieldType { get; set; }
    public object? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public string? ButtonLabel { get; set; }

    /// <summary>
    /// For ListOfValues fields: slash-separated list of options e.g. "Red/Blue/Green".
    /// The first value is always the default selected value.
    /// </summary>
    public string? ListValues { get; set; }

    /// <summary>Parsed list from ListValues. First item is the default.</summary>
    public List<string> GetListOptions() =>
        string.IsNullOrWhiteSpace(ListValues)
            ? new List<string>()
            : ListValues.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    // For button fields: action invoked with the artifact as parameter
    public Func<FlowArtifact, Task>? ButtonAction { get; set; }
}

public class PropertyValue
{
    public string Key { get; set; } = string.Empty;
    public object? Value { get; set; }

    public string AsString() => Value?.ToString() ?? string.Empty;
    public double AsDouble() => Value is double d ? d : double.TryParse(Value?.ToString(), out var r) ? r : 0;
    public DateTime AsDate() => Value is DateTime dt ? dt : DateTime.TryParse(Value?.ToString(), out var r) ? r : DateTime.Today;
}
