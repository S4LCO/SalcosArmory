using SalcosArmory.Config;
using SalcosArmory.Content;
using SalcosArmory.Gameplay;
using SalcosArmory.Traders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Mod;

/// <summary>
/// Registers item templates and the extended Pockets layout before SPT runs profile
/// migrations. This prevents valid SALCO items stored in Pockets from being treated as
/// missing mod items during migrations such as InvalidPocketFix.
/// </summary>
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.Preload)]
public sealed class SalcosArmoryPreload(
    SettingsLoader settingsLoader,
    SoftArmorBalanceService softArmorBalanceService,
    WttContentLoader contentLoader,
    SvmPocketCompatibilityService svmPocketCompatibilityService,
    ExtendedSpecialSlotsService extendedSpecialSlotsService,
    WaylandTraderService waylandTraderService,
    ISptLogger<SalcosArmoryPreload> logger
) : IOnLoad
{
    private bool _loaded;

    public bool IsLoaded => _loaded;

    public ArmoryPaths? Paths { get; private set; }

    public ArmorySettings? Settings { get; private set; }

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var assembly = Assembly.GetExecutingAssembly();
        var paths = ArmoryPaths.FromAssembly(assembly);
        var settings = await settingsLoader.LoadAsync(paths.SettingsFile);
        Paths = paths;
        Settings = settings;

        var softArmorBalance = await settingsLoader.LoadSoftArmorBalanceAsync(
            paths.SoftArmorBalanceFile);
        softArmorBalanceService.Configure(softArmorBalance);

        logger.Info(Log.Line($"Starting {ArmoryInfo.DisplayName} {ArmoryInfo.Version} preload."));

        var results = new List<ModuleResult>
        {
            await contentLoader.LoadAsync(assembly, paths, settings),
            svmPocketCompatibilityService.Prepare(),
            extendedSpecialSlotsService.Load()
        };

        if (settings.LoadWaylandTrader && settings.LoadItems)
        {
            var waylandSettings = await settingsLoader.LoadWaylandAsync(paths.WaylandConfigFile);
            results.Add(waylandTraderService.Load(paths, waylandSettings));
        }
        else
        {
            results.Add(ModuleResult.Skipped(
                "Wayland trader",
                settings.LoadWaylandTrader
                    ? "Disabled because custom items are disabled."
                    : "Disabled in settings."));
        }

        foreach (var result in results)
        {
            var state = result.Success ? result.IsSkipped ? "SKIP" : "OK" : "FAIL";
            logger.Info(Log.Line($"{result.Name}: {state} - {result.Message}"));

            if (!result.Success && settings.StrictMode)
            {
                throw new InvalidOperationException(
                    $"{ArmoryInfo.DisplayName} stopped during {result.Name}: {result.Message}");
            }
        }

        _loaded = true;
        logger.Info(Log.Line("Preload complete; custom item templates are ready for profile migration."));
    }
}
