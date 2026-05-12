namespace FlowChartEditor.Models.Ufa;

/// <summary>
/// Parsed representation of a UFA YAML file.
/// All fields are nullable — missing values fall back to the base artifact.
/// </summary>
public class UfaDefinition
{
    // ── Identity ──────────────────────────────────────────────────────────────
    public string  Name        { get; set; } = string.Empty;
    public string  DisplayName { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;

    // ── Base ──────────────────────────────────────────────────────────────────
    public string  BaseType    { get; set; } = string.Empty;  // e.g. "Process"

    // ── Stencil ───────────────────────────────────────────────────────────────
    public string  StencilGroup { get; set; } = string.Empty;
    public string  StencilTab   { get; set; } = string.Empty;

    // ── Visual ────────────────────────────────────────────────────────────────
    public UfaColor?  Color { get; set; }
    public UfaShape?  Shape { get; set; }

    // ── Ports ─────────────────────────────────────────────────────────────────
    public UfaPorts?  Ports { get; set; }

    // ── Properties ───────────────────────────────────────────────────────────
    public UfaProperties? Properties { get; set; }
}

public class UfaColor
{
    public string? Fill   { get; set; }
    public string? Stroke { get; set; }
    public string? Text   { get; set; }
}

public class UfaShape
{
    /// <summary>Named shape: rectangle, diamond, hexagon, circle,
    /// parallelogram, cylinder, document, note, rounded-rectangle</summary>
    public string? Type    { get; set; }

    /// <summary>Raw SVG path for fully custom shapes.</summary>
    public string? SvgPath { get; set; }
}

public class UfaPorts
{
    /// <summary>Port IDs to remove from the base artifact.</summary>
    public List<string> Remove { get; set; } = new();
}

public class UfaProperties
{
    /// <summary>Base property keys to hide in the Properties modal.</summary>
    public List<string>           Hide { get; set; } = new();

    /// <summary>New properties added by this UFA.</summary>
    public List<UfaPropertyField> Add  { get; set; } = new();
}

public class UfaPropertyField
{
    public string  Key          { get; set; } = string.Empty;
    public string  Label        { get; set; } = string.Empty;
    public string  Type         { get; set; } = "Text";   // Text|Numeric|Date|Button|ListOfValues
    public bool    Required     { get; set; }
    public string? DefaultValue { get; set; }
    public string? ListValues   { get; set; }  // slash-separated for ListOfValues

    /// <summary>Optional dynamic data source e.g. "startTerminal.variables"</summary>
    public string? Source       { get; set; }
}
