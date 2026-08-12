using System.Text.Json;

namespace SalcosArmory.Configurator.Services;

internal static class ConfigLocator
{
    private const string ModFolderName = "SalcosArmory";
    private static readonly string PreferencesFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SalcosArmory",
        "Configurator",
        "settings.json");

    public static string? FindInitialConfigDirectory(IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            if (TryResolve(argument, out var configDirectory))
            {
                return configDirectory;
            }
        }

        var rememberedPath = ReadRememberedPath();
        if (TryResolve(rememberedPath, out var rememberedConfig))
        {
            return rememberedConfig;
        }

        var candidates = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory
        };

        foreach (var candidate in candidates)
        {
            if (TryResolve(candidate, out var configDirectory))
            {
                return configDirectory;
            }

            var current = new DirectoryInfo(candidate);
            for (var level = 0; level < 5 && current is not null; level++, current = current.Parent)
            {
                if (TryResolve(current.FullName, out configDirectory))
                {
                    return configDirectory;
                }
            }
        }

        return null;
    }

    public static bool TryResolve(string? selectedPath, out string configDirectory)
    {
        configDirectory = string.Empty;
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return false;
        }

        string path;
        try
        {
            path = Path.GetFullPath(selectedPath.Trim().Trim('"'));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
        var candidates = new[]
        {
            path,
            Path.Combine(path, "config"),
            Path.Combine(path, ModFolderName, "config"),
            Path.Combine(path, "user", "mods", ModFolderName, "config"),
            Path.Combine(path, "SPT_Runtime", "user", "mods", ModFolderName, "config")
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(Path.Combine(candidate, "settings.json")))
            {
                configDirectory = Path.GetFullPath(candidate);
                return true;
            }
        }

        return false;
    }

    public static void Remember(string configDirectory)
    {
        try
        {
            var directory = Path.GetDirectoryName(PreferencesFile);
            if (directory is null)
            {
                return;
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(
                PreferencesFile,
                JsonSerializer.Serialize(new Preferences { ConfigDirectory = configDirectory }, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Remembering the folder is optional; configuration editing must still work.
        }
    }

    private static string? ReadRememberedPath()
    {
        try
        {
            if (!File.Exists(PreferencesFile))
            {
                return null;
            }

            return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PreferencesFile))?.ConfigDirectory;
        }
        catch
        {
            return null;
        }
    }

    private sealed class Preferences
    {
        public string ConfigDirectory { get; set; } = string.Empty;
    }
}
