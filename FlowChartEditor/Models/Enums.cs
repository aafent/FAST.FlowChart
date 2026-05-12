namespace FlowChartEditor.Models;

public enum ArtifactType
{
    StartTerminal, EndTerminal, Process, Decision,
    InputOutput, Database, Preparation, Document, Note, Connection,
    OnPageConnector, PredefinedProcess, UserDefined, DecisionTable, Switch
}

public enum LineType
{
    Straight,
    Orthogonal,
    Curved,
    Loop        // ← New: amber, circle→arrow, always orthogonal, ↺ icon
}

public enum LoopSide { Left, Right }   // ← which side the loop routes around

public enum ArrowTerminator { None, Arrow, OpenArrow, FilledArrow, Diamond, Circle }
public enum PropertyFieldType { Text, Numeric, Date, Button, ListOfValues }
public enum ValidationSeverity { Error, Warning, Info }
public enum PortSide
{
    Top, Bottom, Left, Right,
    TopLeft, TopRight, BottomLeft, BottomRight
}

public class PortDescriptor
{
    public string        Id        { get; set; } = string.Empty;
    public PortDirection Direction { get; set; }
    public PortSide      Side      { get; set; }
}
// DecisionTable added below — appended to ArtifactType enum manually in the file
