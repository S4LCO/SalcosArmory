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
        return await LoadAsync(filePath, ArmorySettings.Default, "Settings");
    }

    public async Task<RuntimeInjectionSettings> LoadRuntimeInjectionAsync(string filePath)
    {
        return await LoadAsync(filePath, RuntimeInjectionSettings.Default, "Runtime injection settings");
    }

    public async Task<CountermeasureProtocolSettings> LoadCountermeasureProtocolAsync(string filePath)
    {
        return await LoadAsync(filePath, CountermeasureProtocolSettings.Default, "Countermeasure Protocol settings");
    }

    private async Task<T> LoadAsync<T>(string filePath, T defaults, string description)
    {
        if (!File.Exists(filePath))
        {
            logger.Warning(Log.Line($"{description} file not found: {Path.GetFileName(filePath)}. Defaults are used."));
            return defaults;
        }

        var raw = await File.ReadAllTextAsync(filePath);
        if (string.IsNullOrWhiteSpace(raw))
        {
            logger.Warning(Log.Line($"{description} file is empty. Defaults are used."));
            return defaults;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(raw, Options) ?? defaults;
        }
        catch (JsonException ex)
        {
            logger.Error(Log.Line($"{description} could not be read: {ex.Message}"));
            return defaults;
        }
    }
}
