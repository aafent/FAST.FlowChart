using FlowChartEditor.Models;

namespace FlowChartEditor.Services;

public class SyntaxValidator
{
    public ValidationResult Validate(FlowChart chart)
    {
        var result = new ValidationResult();

        var artifacts = chart.Artifacts;
        var connections = chart.Connections;

        // Rule 1: Must have at least one Start Terminal
        var starts = artifacts.Where(a => a.Type == ArtifactType.StartTerminal).ToList();
        if (starts.Count == 0)
            result.Errors.Add(new ValidationMessage
            {
                Severity = ValidationSeverity.Error,
                Message = "The diagram must have at least one Start Terminal."
            });

        // Rule 2: Must have at least one End Terminal
        var ends = artifacts.Where(a => a.Type == ArtifactType.EndTerminal).ToList();
        if (ends.Count == 0)
            result.Errors.Add(new ValidationMessage
            {
                Severity = ValidationSeverity.Error,
                Message = "The diagram must have at least one End Terminal."
            });

        // Rule 3: No dangling connections
        foreach (var conn in connections)
        {
            if (conn.SourceArtifactId == null || conn.TargetArtifactId == null)
            {
                result.Errors.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Error,
                    ArtifactId = conn.Id,
                    ArtifactLabel = conn.ConnectionLabel,
                    Message = $"Connection '{conn.ConnectionLabel}' is not connected at both ends."
                });
                continue;
            }

            if (chart.FindArtifact(conn.SourceArtifactId.Value) == null)
                result.Errors.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Error,
                    ArtifactId = conn.Id,
                    Message = $"Connection '{conn.ConnectionLabel}' has an invalid source artifact."
                });

            if (chart.FindArtifact(conn.TargetArtifactId.Value) == null)
                result.Errors.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Error,
                    ArtifactId = conn.Id,
                    Message = $"Connection '{conn.ConnectionLabel}' has an invalid target artifact."
                });
        }

        // Rule 4: Decision must have exactly 2 outgoing connections
        foreach (var decision in artifacts.Where(a => a.Type == ArtifactType.Decision))
        {
            var outgoing = chart.GetOutgoing(decision.Id).ToList();
            if (outgoing.Count != 2)
                result.Errors.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Error,
                    ArtifactId = decision.Id,
                    ArtifactLabel = decision.Label,
                    Message = $"Decision '{decision.Label}' must have exactly 2 outgoing connections (has {outgoing.Count})."
                });
        }

        // Rule 5: Start Terminal must have no incoming connections
        foreach (var start in starts)
        {
            var incoming = chart.GetIncoming(start.Id).ToList();
            if (incoming.Count > 0)
                result.Warnings.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    ArtifactId = start.Id,
                    ArtifactLabel = start.Label,
                    Message = $"Start Terminal '{start.Label}' has incoming connections."
                });
        }

        // Rule 6: End Terminal must have no outgoing connections
        foreach (var end in ends)
        {
            var outgoing = chart.GetOutgoing(end.Id).ToList();
            if (outgoing.Count > 0)
                result.Warnings.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    ArtifactId = end.Id,
                    ArtifactLabel = end.Label,
                    Message = $"End Terminal '{end.Label}' has outgoing connections."
                });
        }

        // Rule 7: All non-Note artifacts reachable from a Start
        var nonNoteArtifacts = artifacts
            .Where(a => a.Type != ArtifactType.Note)
            .ToList();

        if (starts.Count > 0)
        {
            var reachable = new HashSet<Guid>();
            var queue = new Queue<Guid>(starts.Select(s => s.Id));

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!reachable.Add(current)) continue;
                foreach (var conn in chart.GetOutgoing(current))
                    if (conn.TargetArtifactId.HasValue)
                        queue.Enqueue(conn.TargetArtifactId.Value);
            }

            foreach (var a in nonNoteArtifacts.Where(a => !reachable.Contains(a.Id)))
                result.Warnings.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    ArtifactId = a.Id,
                    ArtifactLabel = a.Label,
                    Message = $"Artifact '{a.Label}' ({a.Type}) is not reachable from any Start Terminal."
                });
        }

        // Rule 8: Non-Note non-Start artifacts should have at least 1 incoming connection
        foreach (var a in nonNoteArtifacts.Where(a =>
            a.Type != ArtifactType.StartTerminal && a.Type != ArtifactType.Connection))
        {
            if (!chart.GetIncoming(a.Id).Any())
                result.Warnings.Add(new ValidationMessage
                {
                    Severity = ValidationSeverity.Warning,
                    ArtifactId = a.Id,
                    ArtifactLabel = a.Label,
                    Message = $"Artifact '{a.Label}' ({a.Type}) has no incoming connections."
                });
        }

        // Rule 9: Dual-output artifacts — only one output port may be used at a time
        var dualOutputTypes = new HashSet<ArtifactType>
        {
            ArtifactType.Process, ArtifactType.PredefinedProcess,
            ArtifactType.Database, ArtifactType.Document,
            ArtifactType.InputOutput, ArtifactType.Preparation
        };

        foreach (var a in artifacts.Where(a => dualOutputTypes.Contains(a.Type)))
        {
            var outgoing = chart.GetOutgoing(a.Id).ToList();
            bool hasBottom = outgoing.Any(c => c.SourcePortId == "out-bottom");
            bool hasRight  = outgoing.Any(c => c.SourcePortId == "out-right");

            if (hasBottom && hasRight)
                result.Errors.Add(new ValidationMessage
                {
                    Severity      = ValidationSeverity.Error,
                    ArtifactId    = a.Id,
                    ArtifactLabel = a.Label,
                    Message       = $"'{a.Label}' ({a.Type}) uses both output ports simultaneously. " +
                                    $"Only one output port (bottom or right) may be connected at a time."
                });
        }

        // ── Loop line validation ────────────────────────────────────────────
        foreach (var conn in connections.Where(c => c.IsLoopLine))
        {
            // Validate source has CanBeLoopBegin
            if (conn.SourceArtifactId.HasValue)
            {
                var src = chart.FindArtifact(conn.SourceArtifactId.Value);
                if (src != null && !src.CanBeLoopBegin)
                    result.Errors.Add(new ValidationMessage
                    {
                        Severity      = ValidationSeverity.Error,
                        ArtifactId    = conn.Id,
                        ArtifactLabel = conn.ConnectionLabel,
                        Message       = $"Loop line source '{src.Label}' ({src.Type}) is not marked as CanBeLoopBegin. " +
                                        $"Open its Properties and enable 'Can be Loop Begin'."
                    });
            }

            // Validate target has CanBeLoopEnd
            if (conn.TargetArtifactId.HasValue)
            {
                var tgt = chart.FindArtifact(conn.TargetArtifactId.Value);
                if (tgt != null && !tgt.CanBeLoopEnd)
                    result.Errors.Add(new ValidationMessage
                    {
                        Severity      = ValidationSeverity.Error,
                        ArtifactId    = conn.Id,
                        ArtifactLabel = conn.ConnectionLabel,
                        Message       = $"Loop line target '{tgt.Label}' ({tgt.Type}) is not marked as CanBeLoopEnd. " +
                                        $"Open its Properties and enable 'Can be Loop End'."
                    });
            }
        }

        return result;
    }
}
