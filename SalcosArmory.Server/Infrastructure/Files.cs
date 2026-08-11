namespace SalcosArmory.Infrastructure;

public static class Files
{
    public static bool HasJson(string folder)
    {
        return Directory.Exists(folder) && EnumerateJson(folder).Any();
    }

    public static IReadOnlyList<string> EnumerateJson(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory
            .GetFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(file =>
                file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
