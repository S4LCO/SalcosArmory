using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Config;

[Injectable(InjectionType.Singleton)]
public sealed class SettingsLoader(ISptLogger<SettingsLoader> logger)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<ArmorySettings> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            logger.Warning(Log.Line($"Settings file not found: {Path.GetFileName(filePath)}. Defaults are used."));
            return ArmorySettings.Default;
        }

        var raw = await File.ReadAllTextAsync(filePath);
        if (string.IsNullOrWhiteSpace(raw))
        {
            logger.Warning(Log.Line("Settings file is empty. Defaults are used."));
            return ArmorySettings.Default;
        }

        try
        {
            return JsonSerializer.Deserialize<ArmorySettings>(raw, Options) ?? ArmorySettings.Default;
        }
        catch (JsonException ex)
        {
            logger.Error(Log.Line($"Settings could not be read: {ex.Message}"));
            return ArmorySettings.Default;
        }
    }
}
