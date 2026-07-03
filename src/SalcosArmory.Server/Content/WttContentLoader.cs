using SalcosArmory.Buffs;
using SalcosArmory.Config;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Content;

[Injectable(InjectionType.Singleton)]
public sealed class WttContentLoader(
    WTTServerCommonLib.WTTServerCommonLib wtt,
    StimBuffService stimBuffService,
    ISptLogger<WttContentLoader> logger
)
{
    public async Task<ModuleResult> LoadAsync(Assembly assembly, ArmoryPaths paths, ArmorySettings settings)
    {
        var loaded = 0;
        var skipped = 0;

        loaded += await Run("Custom items", settings.LoadItems, paths.CustomItems,
            () => wtt.CustomItemServiceExtended.CreateCustomItems(assembly));

        loaded += await Run("Weapon presets", settings.LoadWeaponPresets, paths.CustomWeaponPresets,
            () => wtt.CustomWeaponPresetService.CreateCustomWeaponPresets(assembly));

        loaded += await Run("Hideout recipes", settings.LoadHideoutRecipes, paths.CustomRecipes,
            () => wtt.CustomHideoutRecipeService.CreateHideoutRecipes(assembly));

        loaded += await Run("Locales", settings.LoadLocales, paths.CustomLocales,
            () => wtt.CustomLocaleService.CreateCustomLocales(assembly));

        var buffs = await LoadBuffs();
        if (!buffs.Success)
        {
            return ModuleResult.Failed("Content", buffs.Message);
        }

        if (buffs.IsSkipped)
        {
            skipped++;
        }
        else
        {
            loaded++;
        }

        return loaded == 0
            ? ModuleResult.Skipped("Content", "No enabled content folders had JSON files.")
            : ModuleResult.Ok("Content", $"Registered {loaded} content folder(s). Skipped {skipped} empty/disabled folder(s).");

        async Task<int> Run(string name, bool enabled, string folder, Func<Task> action)
        {
            if (!enabled)
            {
                skipped++;
                logger.Info(Log.Line($"{name}: disabled."));
                return 0;
            }

            if (!Files.HasJson(folder))
            {
                skipped++;
                logger.Info(Log.Line($"{name}: no JSON files."));
                return 0;
            }

            await action();
            logger.Info(Log.Line($"{name}: registered."));
            return 1;
        }

        async Task<ModuleResult> LoadBuffs()
        {
            if (!settings.LoadBuffs)
            {
                logger.Info(Log.Line("Buffs: disabled."));
                return ModuleResult.Skipped("Buffs", "Disabled in settings.");
            }

            var result = await stimBuffService.LoadAsync(assembly, paths);

            if (result.IsSkipped)
            {
                logger.Info(Log.Line($"Buffs: {result.Message}"));
            }
            else if (result.Success)
            {
                logger.Info(Log.Line($"Buffs: {result.Message}"));
            }

            return result;
        }
    }
}