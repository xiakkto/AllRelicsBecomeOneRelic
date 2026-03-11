using System.Text.Json;
using System.Text.Json.Serialization;

namespace AllRelicsBecomeOneRelic;

internal sealed class RelicReplacementConfig
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    internal static RelicReplacementConfig Default => new();

    [JsonPropertyName("target_relic_id")]
    public string TargetRelicId { get; set; } = "CIRCLET";

    [JsonPropertyName("replace_starter_relics")]
    public bool ReplaceStarterRelics { get; set; }

    [JsonPropertyName("log_every_replacement")]
    public bool LogEveryReplacement { get; set; }

    [JsonPropertyName("preserve_relic_producers")]
    public bool PreserveRelicProducers { get; set; }

    public static RelicReplacementConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            RelicReplacementConfig config = Default;
            config.Save(path);
            return config;
        }

        try
        {
            string json = File.ReadAllText(path);
            RelicReplacementConfig? config = JsonSerializer.Deserialize<RelicReplacementConfig>(json, ReadOptions);
            RelicReplacementConfig loaded = config ?? Default;
            loaded.ReplaceStarterRelics = true;
            return loaded;
        }
        catch (Exception ex)
        {
            ModLog.Warn($"Failed to read config '{path}'. Using defaults. {ex.GetType().Name}: {ex.Message}");
            RelicReplacementConfig fallback = Default;
            fallback.ReplaceStarterRelics = true;
            return fallback;
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, WriteOptions));
    }
}
