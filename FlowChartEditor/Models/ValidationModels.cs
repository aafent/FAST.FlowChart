namespace FlowChartEditor.Models;

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<ValidationMessage> Errors { get; set; } = new();
    public List<ValidationMessage> Warnings { get; set; } = new();
}

public class ValidationMessage
{
    public ValidationSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? ArtifactId { get; set; }
    public string ArtifactLabel { get; set; } = string.Empty;
}
