using System.Net.Http.Json;
using System.Text.Json;
using FlowChartEditor.Models;
using FlowChartEditor.Models.Ufa;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FlowChartEditor.Services;

/// <summary>
/// Loads UFA definitions from wwwroot/stencils/ at application startup.
/// Reads manifest.json then fetches each enabled YAML file.
/// Validates names for uniqueness and rejects duplicates.
/// </summary>
public class UfaLoader
{
    private readonly HttpClient _http;

    // All successfully loaded UFA definitions — keyed by name (case-insensitive)
    public Dictionary<string, UfaDefinition> Definitions { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Load errors/warnings for diagnostics
    public List<string> LoadErrors   { get; } = new();
    public List<string> LoadWarnings { get; } = new();

    public bool IsLoaded { get; private set; }

    private static readonly IDeserializer YamlDeserializer =
        new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

    public UfaLoader(HttpClient http) => _http = http;

    public async Task LoadAsync()
    {
        Definitions.Clear();
        LoadErrors.Clear();
        LoadWarnings.Clear();

        // 1 — Load manifest
        UfaManifest? manifest;
        try
        {
            manifest = await _http.GetFromJsonAsync<UfaManifest>("stencils/manifest.json");
        }
        catch (Exception ex)
        {
            LoadWarnings.Add($"No UFA manifest found (stencils/manifest.json): {ex.Message}");
            IsLoaded = true;
            return;
        }

        if (manifest == null || manifest.Artifacts.Count == 0)
        {
            IsLoaded = true;
            return;
        }

        // 2 — Load each enabled YAML file
        foreach (var entry in manifest.Artifacts.Where(e => e.Enabled))
        {
            try
            {
                var yaml = await _http.GetStringAsync($"stencils/{entry.File}");

                UfaDefinition def;
                try
                {
                    def = YamlDeserializer.Deserialize<UfaDefinition>(yaml);
                }
                catch (Exception yamlEx)
                {
                    LoadErrors.Add($"[{entry.File}] YAML parse error: {yamlEx.Message}");
                    continue;
                }

                // Validate required fields
                if (string.IsNullOrWhiteSpace(def.Name))
                {
                    LoadErrors.Add($"[{entry.File}] Missing required field: name");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(def.BaseType))
                {
                    LoadErrors.Add($"[{entry.File}] Missing required field: baseType");
                    continue;
                }

                // Validate base type exists
                if (!Enum.TryParse<ArtifactType>(def.BaseType, true, out _))
                {
                    LoadErrors.Add($"[{entry.File}] Unknown baseType '{def.BaseType}'");
                    continue;
                }

                // Check name uniqueness — reject duplicates
                if (Definitions.ContainsKey(def.Name))
                {
                    LoadErrors.Add($"[{entry.File}] Duplicate UFA name '{def.Name}' — skipped");
                    continue;
                }

                // Also check against built-in artifact display names
                var builtInNames = Enum.GetValues<ArtifactType>()
                    .Select(t => ArtifactFactory.GetDisplayName(t))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (builtInNames.Contains(def.Name))
                {
                    LoadErrors.Add($"[{entry.File}] Name '{def.Name}' conflicts with a built-in artifact — skipped");
                    continue;
                }

                // Apply defaults for missing stencil fields
                if (string.IsNullOrWhiteSpace(def.StencilGroup))
                    def.StencilGroup = "USER DEFINED";
                if (string.IsNullOrWhiteSpace(def.StencilTab))
                    def.StencilTab = "General";
                if (string.IsNullOrWhiteSpace(def.DisplayName))
                    def.DisplayName = def.Name;

                Definitions[def.Name] = def;
            }
            catch (Exception ex)
            {
                LoadErrors.Add($"[{entry.File}] Failed to load: {ex.Message}");
            }
        }

        IsLoaded = true;
    }

    /// <summary>Creates a UserDefinedArtifact instance from a definition name.</summary>
    public UserDefinedArtifact? CreateInstance(string ufaName, double x = 0, double y = 0)
    {
        if (!Definitions.TryGetValue(ufaName, out var def)) return null;

        if (!Enum.TryParse<ArtifactType>(def.BaseType, true, out var baseType)) return null;
        var baseInstance = ArtifactFactory.Create(baseType, x, y);

        var artifact = new UserDefinedArtifact { X = x, Y = y };
        artifact.Initialise(def, baseInstance);
        artifact.InitialiseDefaults();
        return artifact;
    }

    /// <summary>Returns stencil group infos for all loaded UFAs — merged with built-ins.</summary>
    public List<UfaStencilGroup> GetUfaStencilGroups()
    {
        return Definitions.Values
            .GroupBy(d => d.StencilGroup)
            .Select(g => new UfaStencilGroup(
                g.Key,
                g.GroupBy(d => d.StencilTab)
                 .Select(t => new UfaStencilTab(
                     t.Key,
                     t.Select(d => new UfaStencilItem(d.Name, d.DisplayName, d)).ToList()))
                 .ToList()))
            .ToList();
    }
}

public record UfaStencilGroup(string Name, List<UfaStencilTab> Tabs);
public record UfaStencilTab(string Name, List<UfaStencilItem> Items);
public record UfaStencilItem(string UfaName, string DisplayName, UfaDefinition Definition);
