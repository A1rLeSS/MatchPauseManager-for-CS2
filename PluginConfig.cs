using System.Text.Json;
using System.Text.Json.Serialization;

namespace MatchPauseManager;

/// <summary>
/// Plugin settings. Stored as config.json next to the plugin assembly
/// (addons/counterstrikesharp/plugins/MatchPauseManager/config.json).
/// </summary>
public sealed class PluginConfig
{
    /// <summary>Tactical timeout duration in seconds.</summary>
    public int TacticalTime { get; set; } = 30;

    /// <summary>Tactical timeouts per team during regulation.</summary>
    public int TacticalCount { get; set; } = 3;

    /// <summary>Tactical timeouts per team per overtime segment.</summary>
    public int OvertimeCount { get; set; } = 1;

    /// <summary>
    /// 1 = a technical pause only invalidates the round when the round already
    /// produced damage. 0 = always treat the round as invalid.
    /// </summary>
    public bool TechnicalDamageGate { get; set; } = true;

    /// <summary>Allow the referee to restore an invalidated round.</summary>
    public bool RewindEnabled { get; set; } = true;

    /// <summary>
    /// Best effort removal of the invalidated round from the per-round stats
    /// that feed the scoreboard (CS2 does not expose writable K/D/A totals).
    /// </summary>
    public bool RewindPlayerStats { get; set; } = true;

    /// <summary>Broadcast text style: 0 = international, 1 = Chinese, 2 = international + rules.</summary>
    public int TextStyle { get; set; } = 0;

    /// <summary>Warn players who chat during a technical pause.</summary>
    public bool WarnChatDuringTechnical { get; set; } = true;

    [JsonIgnore]
    public string FilePath { get; private set; } = string.Empty;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static PluginConfig Load(string moduleDirectory)
    {
        var path = Path.Combine(moduleDirectory, "config.json");
        PluginConfig config;

        try
        {
            if (File.Exists(path))
            {
                config = JsonSerializer.Deserialize<PluginConfig>(File.ReadAllText(path), Options) ?? new PluginConfig();
            }
            else
            {
                config = new PluginConfig();
                File.WriteAllText(path, JsonSerializer.Serialize(config, Options));
            }
        }
        catch
        {
            config = new PluginConfig();
        }

        config.FilePath = path;
        config.Normalize();
        return config;
    }

    private void Normalize()
    {
        TacticalTime = Math.Clamp(TacticalTime, 1, 300);
        TacticalCount = Math.Clamp(TacticalCount, 0, 20);
        OvertimeCount = Math.Clamp(OvertimeCount, 0, 20);
        TextStyle = Math.Clamp(TextStyle, 0, 2);
    }
}
