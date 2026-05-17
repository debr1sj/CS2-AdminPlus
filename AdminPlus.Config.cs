using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AdminPlus;

public partial class AdminPlus
{
    public const string MenuConfigFileName = "adminplus_menu_config.json";

    private static AdminPlusMenuConfigFile _menuConfigFile = AdminPlusMenuConfigFile.CreateDefault();

    internal static void LogWarn(string message)
        => Console.WriteLine($"[AdminPlus] {message}");

    internal static void LogError(string message)
        => Console.WriteLine($"[AdminPlus] ERROR: {message}");

    private void LoadMenuConfigFile()
    {
        try
        {
            Directory.CreateDirectory(ModuleDirectory);
            var path = Path.Combine(ModuleDirectory, MenuConfigFileName);

            if (!File.Exists(path))
            {
                _menuConfigFile = AdminPlusMenuConfigFile.CreateDefault();
                File.WriteAllText(path, JsonSerializer.Serialize(_menuConfigFile, MenuConfigJsonOptions));
                ApplyMenuConfig(_menuConfigFile);
                LogWarn($"Created {MenuConfigFileName}");
                return;
            }

            _menuConfigFile = JsonSerializer.Deserialize<AdminPlusMenuConfigFile>(File.ReadAllText(path), MenuConfigJsonOptions)
                ?? AdminPlusMenuConfigFile.CreateDefault();
            ApplyMenuConfig(_menuConfigFile);
        }
        catch (Exception ex)
        {
            LogError($"Menu config load failed: {ex.Message}");
            _menuConfigFile = AdminPlusMenuConfigFile.CreateDefault();
            ApplyMenuConfig(_menuConfigFile);
        }
    }

    private static readonly JsonSerializerOptions MenuConfigJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}

/// <summary>Menu key bindings. Values: W, S, E, A, D, R, Tab.</summary>
internal sealed class AdminPlusMenuConfigFile
{
    [JsonPropertyName("moveUp")]
    public string MoveUp { get; set; } = "W";

    [JsonPropertyName("moveDown")]
    public string MoveDown { get; set; } = "S";

    [JsonPropertyName("select")]
    public string Select { get; set; } = "E";

    [JsonPropertyName("back")]
    public string Back { get; set; } = "A";

    [JsonPropertyName("exit")]
    public string Exit { get; set; } = "R";

    [JsonPropertyName("sliderLeft")]
    public string SliderLeft { get; set; } = "A";

    [JsonPropertyName("sliderRight")]
    public string SliderRight { get; set; } = "D";

    public static AdminPlusMenuConfigFile CreateDefault() => new();
}
