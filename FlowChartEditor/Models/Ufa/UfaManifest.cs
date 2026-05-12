namespace FlowChartEditor.Models.Ufa;

/// <summary>Root manifest file — wwwroot/stencils/manifest.json</summary>
public class UfaManifest
{
    public string            Version   { get; set; } = "1.0";
    public string            Author    { get; set; } = string.Empty;
    public List<UfaManifestEntry> Artifacts { get; set; } = new();
}

public class UfaManifestEntry
{
    public string File    { get; set; } = string.Empty;
    public bool   Enabled { get; set; } = true;
}
