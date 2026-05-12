using System.Text.Json.Serialization;
using FlowChartEditor.Models;
using FlowChartEditor.Models.Artifacts;

namespace FlowChartEditor.Models.Ufa;

/// <summary>
/// A runtime artifact instance created from a UFA YAML definition.
/// Delegates rendering and port logic to its base artifact,
/// applying color/shape/port overrides from the definition.
/// </summary>
public class UserDefinedArtifact : FlowArtifact
{
    /// <summary>The UFA definition this instance was created from.</summary>
    [JsonIgnore] public UfaDefinition? Definition { get; set; }

    /// <summary>Name of the UFA definition (used to re-link after deserialisation).</summary>
    public string UfaName { get; set; } = string.Empty;

    // Resolved colors (from definition or base)
    [JsonIgnore] public string ResolvedFillColor   { get; private set; } = "#ffffff";
    [JsonIgnore] public string ResolvedStrokeColor { get; private set; } = "#000000";
    [JsonIgnore] public string ResolvedTextColor   { get; private set; } = "#000000";

    // The underlying base artifact used for shape/port rendering
    [JsonIgnore] private FlowArtifact? _baseInstance;

    public UserDefinedArtifact() { Type = ArtifactType.UserDefined; }

    /// <summary>Initialise from a UFA definition — resolves colors, shape, ports.</summary>
    public void Initialise(UfaDefinition def, FlowArtifact baseInstance)
    {
        Definition   = def;
        UfaName      = def.Name;
        _baseInstance = baseInstance;

        // Inherit loop participation from base artifact
        CanBeLoopBegin = baseInstance.CanBeLoopBegin;
        CanBeLoopEnd   = baseInstance.CanBeLoopEnd;

        // Copy dimensions from base
        Width  = baseInstance.Width;
        Height = baseInstance.Height;

        // Stencil metadata
        StencilGroup = def.StencilGroup;
        StencilTab   = def.StencilTab;

        // Resolve colors — UFA overrides base if defined
        var baseFill   = GetBaseColor(baseInstance, "fill");
        var baseStroke = GetBaseColor(baseInstance, "stroke");
        var baseText   = GetBaseColor(baseInstance, "text");

        ResolvedFillColor   = def.Color?.Fill   ?? baseFill;
        ResolvedStrokeColor = def.Color?.Stroke ?? baseStroke;
        ResolvedTextColor   = def.Color?.Text   ?? baseText;

        // Default label from displayName
        if (string.IsNullOrEmpty(Label))
            Label = def.DisplayName;
    }

    public override List<PortDescriptor> GetPortDescriptors()
    {
        if (_baseInstance == null) return new();
        var ports  = _baseInstance.GetPortDescriptors();
        var remove = Definition?.Ports?.Remove ?? new List<string>();
        return ports.Where(p => !remove.Contains(p.Id)).ToList();
    }

    public override List<PropertyFieldDefinition> GetPropertyDefinitions()
    {
        if (_baseInstance == null) return new();

        var baseDefs = _baseInstance.GetPropertyDefinitions();
        var hidden   = Definition?.Properties?.Hide ?? new List<string>();

        // Start with base props minus hidden ones
        var result = baseDefs
            .Where(d => !hidden.Contains(d.Key))
            .ToList();

        // Add UFA-specific properties
        if (Definition?.Properties?.Add != null)
        {
            foreach (var f in Definition.Properties.Add)
            {
                result.Add(new PropertyFieldDefinition
                {
                    Key          = f.Key,
                    Label        = f.Label,
                    FieldType    = ParseFieldType(f.Type),
                    IsRequired   = f.Required,
                    DefaultValue = f.DefaultValue,
                    ListValues   = f.ListValues
                });
            }
        }

        return result;
    }

    public override string RenderSvgShape()
    {
        if (_baseInstance == null) return string.Empty;

        try
        {
            SyncToBase();

            if (Definition?.Shape?.SvgPath != null)
                return RenderCustomPath(Definition.Shape.SvgPath);

            if (Definition?.Shape?.Type != null)
                return RenderNamedShape(Definition.Shape.Type);

            return InjectColors(_baseInstance.RenderSvgShape());
        }
        catch
        {
            return _baseInstance.RenderSvgShape();
        }
    }

    public override string RenderStencilShape()
    {
        if (_baseInstance == null) return string.Empty;

        try
        {
            if (Definition?.Shape?.Type != null)
                return RenderNamedStencilShape(Definition.Shape.Type);

            return InjectColors(_baseInstance.RenderStencilShape());
        }
        catch
        {
            return _baseInstance.RenderStencilShape();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SyncToBase()
    {
        _baseInstance!.X            = X;
        _baseInstance.Y             = Y;
        _baseInstance.Width         = Width;
        _baseInstance.Height        = Height;
        _baseInstance.Label         = Label;
        _baseInstance.IsSelected    = IsSelected;
        _baseInstance.IsEditingLabel = IsEditingLabel;
    }

    private string InjectColors(string svg)
    {
        if (string.IsNullOrEmpty(svg)) return svg;
        if (_baseInstance == null) return svg;

        try
        {
            var baseFill   = GetBaseColor(_baseInstance, "fill");
            var baseStroke = GetBaseColor(_baseInstance, "stroke");
            var baseText   = GetBaseColor(_baseInstance, "text");

            return svg
                .Replace($"fill=\"{baseFill}\"",    $"fill=\"{ResolvedFillColor}\"")
                .Replace($"stroke=\"{baseStroke}\"", $"stroke=\"{ResolvedStrokeColor}\"")
                .Replace($"fill=\"{baseText}\"",     $"fill=\"{ResolvedTextColor}\"");
        }
        catch
        {
            return svg;
        }
    }

    private string RenderCustomPath(string svgPath)
    {
        var cx     = X + Width / 2;
        var cy     = Y + Height / 2;
        var stroke = IsSelected ? "#6366f1" : ResolvedStrokeColor;
        var sw     = IsSelected ? "2.5" : "1.5";
        var sel    = IsSelected ? SvgHelper.SelectionRect(X, Y, Width, Height) : "";

        return $"<path d=\"{svgPath}\" fill=\"{ResolvedFillColor}\" stroke=\"{stroke}\" " +
               $"stroke-width=\"{sw}\" class=\"fc-artifact-shape\"/>" +
               sel +
               (IsEditingLabel ? "" : SvgHelper.TextLabel(cx, cy, Label, ResolvedTextColor));
    }

    private string RenderNamedShape(string shapeName)
    {
        var cx     = X + Width / 2;
        var cy     = Y + Height / 2;
        var stroke = IsSelected ? "#6366f1" : ResolvedStrokeColor;
        var sw     = IsSelected ? "2.5" : "1.5";
        var sel    = IsSelected ? SvgHelper.SelectionRect(X, Y, Width, Height) : "";
        var label  = IsEditingLabel ? "" : SvgHelper.TextLabel(cx, cy, Label, ResolvedTextColor);

        var shape = shapeName.ToLowerInvariant() switch
        {
            "rectangle" =>
                $"<rect x=\"{SvgHelper.F(X)}\" y=\"{SvgHelper.F(Y)}\" width=\"{SvgHelper.F(Width)}\" height=\"{SvgHelper.F(Height)}\" rx=\"4\" " +
                $"fill=\"{ResolvedFillColor}\" stroke=\"{stroke}\" stroke-width=\"{sw}\" class=\"fc-artifact-shape\"/>",

            "rounded-rectangle" =>
                $"<rect x=\"{SvgHelper.F(X)}\" y=\"{SvgHelper.F(Y)}\" width=\"{SvgHelper.F(Width)}\" height=\"{SvgHelper.F(Height)}\" rx=\"{SvgHelper.F(Height/2)}\" " +
                $"fill=\"{ResolvedFillColor}\" stroke=\"{stroke}\" stroke-width=\"{sw}\" class=\"fc-artifact-shape\"/>",

            "diamond" =>
                $"<polygon points=\"{SvgHelper.F(cx)},{SvgHelper.F(Y)} {SvgHelper.F(X+Width)},{SvgHelper.F(cy)} {SvgHelper.F(cx)},{SvgHelper.F(Y+Height)} {SvgHelper.F(X)},{SvgHelper.F(cy)}\" " +
                $"fill=\"{ResolvedFillColor}\" stroke=\"{stroke}\" stroke-width=\"{sw}\" class=\"fc-artifact-shape\"/>",

            "hexagon" =>
                $"<polygon points=\"{SvgHelper.F(X+Width*0.18)},{SvgHelper.F(Y)} {SvgHelper.F(X+Width*0.82)},{SvgHelper.F(Y)} {SvgHelper.F(X+Width)},{SvgHelper.F(cy)} {SvgHelper.F(X+Width*0.82)},{SvgHelper.F(Y+Height)} {SvgHelper.F(X+Width*0.18)},{SvgHelper.F(Y+Height)} {SvgHelper.F(X)},{SvgHelper.F(cy)}\" " +
                $"fill=\"{ResolvedFillColor}\" stroke=\"{stroke}\" stroke-width=\"{sw}\" class=\"fc-artifact-shape\"/>",

            "circle" =>
                $"<circle cx=\"{SvgHelper.F(cx)}\" cy=\"{SvgHelper.F(cy)}\" r=\"{SvgHelper.F(Math.Min(Width,Height)/2)}\" " +
                $"fill=\"{ResolvedFillColor}\" stroke=\"{stroke}\" stroke-width=\"{sw}\" class=\"fc-artifact-shape\"/>",

            "parallelogram" =>
                $"<polygon points=\"{SvgHelper.F(X+Width*0.15)},{SvgHelper.F(Y)} {SvgHelper.F(X+Width)},{SvgHelper.F(Y)} {SvgHelper.F(X+Width*0.85)},{SvgHelper.F(Y+Height)} {SvgHelper.F(X)},{SvgHelper.F(Y+Height)}\" " +
                $"fill=\"{ResolvedFillColor}\" stroke=\"{stroke}\" stroke-width=\"{sw}\" class=\"fc-artifact-shape\"/>",

            _ => $"<rect x=\"{SvgHelper.F(X)}\" y=\"{SvgHelper.F(Y)}\" width=\"{SvgHelper.F(Width)}\" height=\"{SvgHelper.F(Height)}\" rx=\"4\" " +
                 $"fill=\"{ResolvedFillColor}\" stroke=\"{stroke}\" stroke-width=\"{sw}\" class=\"fc-artifact-shape\"/>"
        };

        return shape + sel + label;
    }

    private string RenderNamedStencilShape(string shapeName)
    {
        const double x = 4, y = 4, w = 44, h = 28;
        var cx = x + w / 2; var cy = y + h / 2;

        return shapeName.ToLowerInvariant() switch
        {
            "diamond" =>
                $"<polygon points=\"{SvgHelper.F(cx)},{SvgHelper.F(y)} {SvgHelper.F(x+w)},{SvgHelper.F(cy)} {SvgHelper.F(cx)},{SvgHelper.F(y+h)} {SvgHelper.F(x)},{SvgHelper.F(cy)}\" " +
                $"fill=\"{ResolvedFillColor}\" stroke=\"{ResolvedStrokeColor}\" stroke-width=\"1.5\"/>",

            "circle" =>
                $"<circle cx=\"{SvgHelper.F(cx)}\" cy=\"{SvgHelper.F(cy)}\" r=\"{SvgHelper.F(Math.Min(w,h)/2)}\" " +
                $"fill=\"{ResolvedFillColor}\" stroke=\"{ResolvedStrokeColor}\" stroke-width=\"1.5\"/>",

            "hexagon" =>
                $"<polygon points=\"{SvgHelper.F(x+w*0.18)},{SvgHelper.F(y)} {SvgHelper.F(x+w*0.82)},{SvgHelper.F(y)} {SvgHelper.F(x+w)},{SvgHelper.F(cy)} {SvgHelper.F(x+w*0.82)},{SvgHelper.F(y+h)} {SvgHelper.F(x+w*0.18)},{SvgHelper.F(y+h)} {SvgHelper.F(x)},{SvgHelper.F(cy)}\" " +
                $"fill=\"{ResolvedFillColor}\" stroke=\"{ResolvedStrokeColor}\" stroke-width=\"1.5\"/>",

            _ =>
                $"<rect x=\"{SvgHelper.F(x)}\" y=\"{SvgHelper.F(y)}\" width=\"{SvgHelper.F(w)}\" height=\"{SvgHelper.F(h)}\" rx=\"4\" " +
                $"fill=\"{ResolvedFillColor}\" stroke=\"{ResolvedStrokeColor}\" stroke-width=\"1.5\"/>"
        } + SvgHelper.TextLabel(cx, cy, Definition?.DisplayName ?? Label, ResolvedTextColor, 7);
    }

    private static string GetBaseColor(FlowArtifact a, string which)
    {
        try
        {
            var type = a.GetType();
            var fieldName = which switch
            {
                "fill"   => "FillColor",
                "stroke" => "StrokeColor",
                _        => "TextColor"
            };
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static);
            return field?.GetValue(null)?.ToString() ?? "#ffffff";
        }
        catch
        {
            return which == "text" ? "#1e293b" : "#ffffff";
        }
    }

    private static PropertyFieldType ParseFieldType(string t) => t.ToLower() switch
    {
        "numeric"      => PropertyFieldType.Numeric,
        "date"         => PropertyFieldType.Date,
        "button"       => PropertyFieldType.Button,
        "listofvalues" => PropertyFieldType.ListOfValues,
        _              => PropertyFieldType.Text
    };
}
