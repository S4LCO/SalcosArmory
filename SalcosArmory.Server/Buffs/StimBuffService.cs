using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Buffs;

[Injectable(InjectionType.Singleton)]
public sealed class StimBuffService(
    WTTServerCommonLib.WTTServerCommonLib wtt,
    StimBuffValidator validator,
    ISptLogger<StimBuffService> logger
)
{
    public async Task<ModuleResult> LoadAsync(Assembly assembly, ArmoryPaths paths)
    {
        var files = GetBuffFiles(paths.CustomBuffs);

        if (files.Length == 0)
        {
            return ModuleResult.Skipped("Buffs", "No custom buff files found.");
        }

        var stimKeys = 0;
        var buffEntries = 0;

        foreach (var file in files)
        {
            var check = await validator.ValidateAsync(file);

            if (!check.Success)
            {
                logger.Error(Log.Line($"Buffs: {check.Message}"));
                return ModuleResult.Failed("Buffs", $"Validation failed in '{Path.GetFileName(file)}'.");
            }

            stimKeys += check.StimKeys;
            buffEntries += check.BuffEntries;

            logger.Info(Log.Line(
                $"Buffs: {Path.GetFileName(file)} OK ({check.StimKeys} stim key(s), {check.BuffEntries} buff entries)."
            ));
        }

        var relativePath = Path.GetRelativePath(paths.Root, paths.CustomBuffs);

        try
        {
            await wtt.CustomBuffService.CreateCustomBuffs(assembly, relativePath);
        }
        catch (Exception ex)
        {
            logger.Error(Log.Line($"Buffs: WTT registration failed: {ex.Message}"));
            return ModuleResult.Failed("Buffs", "WTT registration failed. Check the previous log line.");
        }

        return ModuleResult.Ok(
            "Buffs",
            $"Registered {stimKeys} stim key(s) with {buffEntries} buff entries from {files.Length} file(s)."
        );
    }

    private static string[] GetBuffFiles(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory
            .GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(file =>
                file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}