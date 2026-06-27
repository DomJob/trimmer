using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trimmer.Build;

/// <summary>Describes the contents of a compiled <c>.trm</c> bundle.</summary>
public sealed class BundleManifest
{
    public const string FileName = "manifest.json";

    /// <summary>The compiled pages assembly file name (e.g. <c>pages.dll</c>).</summary>
    public string Assembly { get; set; } = "pages.dll";

    /// <summary>Maps a normalized request route to the generated page type's full name.</summary>
    public Dictionary<string, string> Routes { get; set; } = new();

    /// <summary>NuGet assembly file names placed under <c>lib/</c>.</summary>
    public List<string> Libraries { get; set; } = [];

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static BundleManifest FromJson(string json) =>
        JsonSerializer.Deserialize<BundleManifest>(json, Options)
        ?? throw new InvalidOperationException("Invalid bundle manifest.");
}
