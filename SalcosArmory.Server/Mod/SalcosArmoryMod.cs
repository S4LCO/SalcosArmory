using SalcosArmory.Compat;
using SalcosArmory.Config;
using SalcosArmory.Countermeasures;
using SalcosArmory.MedicalMerge;
using SalcosArmory.Runtime;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Mod;

[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad)]
public sealed class SalcosArmoryMod(
    SettingsLoader settingsLoader,
    SalcosArmoryPreload preload,
    CompatService compatService,
    RuntimeInjectionService runtimeInjectionService,
    CountermeasureProtocolService countermeasureProtocolService,
    MedicalMergeRegistration medicalMergeRegistration,
    ISptLogger<SalcosArmoryMod> logger
) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        if (!LoadGuard.Enter())
        {
            logger.Warning(Log.Line("Load skipped because the mod is already active."));
            return;
        }

        if (!preload.IsLoaded)
        {
            await preload.OnLoadAsync(cancellationToken);
        }

        var paths = preload.Paths
            ?? throw new InvalidOperationException("SALCO's ARMORY preload did not initialize its paths.");
        var settings = preload.Settings
            ?? throw new InvalidOperationException("SALCO's ARMORY preload did not initialize its settings.");
        var results = new List<ModuleResult>();

        if (settings.LoadCompat)
        {
            results.Add(await compatService.LoadAsync(paths, settings.Debug));
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

        if (settings.LoadCountermeasureProtocol)
        {
            var countermeasureSettings = await settingsLoader.LoadCountermeasureProtocolAsync(
                paths.CountermeasureProtocolFile);
            results.Add(countermeasureProtocolService.Load(countermeasureSettings, paths, settings.Debug));
        }
        else
        {
            results.Add(ModuleResult.Skipped("Countermeasure Protocol", "Disabled in settings."));
        }

        StartupReporter.Report(logger, results, settings);

        logger.Success(Log.Line("Loaded."));
    }
}
