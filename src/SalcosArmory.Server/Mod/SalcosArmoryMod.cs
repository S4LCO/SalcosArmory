using SalcosArmory.Compat;
using SalcosArmory.Config;
using SalcosArmory.Content;
using SalcosArmory.MedicalMerge;
using SalcosArmory.Runtime;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Mod;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostDBModLoader + 20)]
public sealed class SalcosArmoryMod(
    SettingsLoader settingsLoader,
    WttContentLoader contentLoader,
    CompatService compatService,
    RuntimeInjectionService runtimeInjectionService,
    MedicalMergeRegistration medicalMergeRegistration,
    ISptLogger<SalcosArmoryMod> logger
) : IOnLoad
{
    public async Task OnLoad()
    {
        if (!LoadGuard.Enter())
        {
            logger.Warning(Log.Line("Load skipped because the mod is already active."));
            return;
        }

        var assembly = Assembly.GetExecutingAssembly();
        var paths = ArmoryPaths.FromAssembly(assembly);
        var settings = await settingsLoader.LoadAsync(paths.SettingsFile);

        logger.Info(Log.Line($"Starting {ArmoryInfo.DisplayName} {ArmoryInfo.Version}."));

        var results = new List<ModuleResult>
        {
            await contentLoader.LoadAsync(assembly, paths, settings)
        };

        if (settings.LoadCompat)
        {
            results.Add(await compatService.LoadAsync(paths));
        }
        else
        {
            results.Add(ModuleResult.Skipped("Compat", "Disabled in settings."));
        }

        if (settings.LoadRuntimeInjection)
        {
            var runtimeSettings = await settingsLoader.LoadRuntimeInjectionAsync(paths.RuntimeInjectionFile);
            results.Add(runtimeInjectionService.Load(runtimeSettings, settings.Debug));
        }
        else
        {
            results.Add(ModuleResult.Skipped("Runtime injection", "Disabled in settings."));
        }

        results.Add(settings.LoadMedicalMerge
            ? medicalMergeRegistration.Register()
            : ModuleResult.Skipped("Medical merge", "Disabled in settings."));

        foreach (var result in results)
        {
            var state = result.Success ? result.IsSkipped ? "SKIP" : "OK" : "FAIL";
            logger.Info(Log.Line($"{result.Name}: {state} - {result.Message}"));

            if (!result.Success && settings.StrictMode)
            {
                throw new InvalidOperationException($"{ArmoryInfo.DisplayName} stopped during {result.Name}: {result.Message}");
            }
        }

        logger.Success(Log.Line("Loaded."));
    }
}
