using FlowChartEditor.Models.Artifacts;

namespace FlowChartEditor.Models;

public class FlowChart
{
    public Guid     Id         { get; set; } = Guid.NewGuid();
    public string   Title      { get; set; } = "Untitled Flow Chart";
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    // Polymorphic list — JSON serialiser uses $type discriminator
    public List<FlowArtifact>      Artifacts   { get; set; } = new();
    public List<ConnectionArtifact> Connections { get; set; } = new();

    public double CanvasWidth  { get; set; } = 4000;
    public double CanvasHeight { get; set; } = 3000;
    public int    GridSize     { get; set; } = 20;

    public FlowArtifact?       FindArtifact(Guid id)  => Artifacts.FirstOrDefault(a => a.Id == id);
    public ConnectionArtifact? FindConnection(Guid id) => Connections.FirstOrDefault(c => c.Id == id);

    public IEnumerable<ConnectionArtifact> GetConnectionsFor(Guid id) =>
        Connections.Where(c => c.SourceArtifactId == id || c.TargetArtifactId == id);

    public IEnumerable<ConnectionArtifact> GetOutgoing(Guid id) =>
        Connections.Where(c => c.SourceArtifactId == id);

    public IEnumerable<ConnectionArtifact> GetIncoming(Guid id) =>
        Connections.Where(c => c.TargetArtifactId == id);
}
