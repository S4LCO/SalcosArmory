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
        softArmorBalanceService.Configure(softArmorBalance, settings.Debug);

        logger.Info(Log.Line($"Loading {ArmoryInfo.DisplayName} {ArmoryInfo.Version}..."));

        var results = new List<ModuleResult>
        {
            await contentLoader.LoadAsync(assembly, paths, settings),
            settings.LoadExtendedSpecialSlots
                ? svmPocketCompatibilityService.Prepare()
                : ModuleResult.Skipped(
                    "SVM pocket compatibility",
                    "Disabled because extended special slots are disabled."),
            settings.LoadExtendedSpecialSlots
                ? extendedSpecialSlotsService.Load()
                : ModuleResult.Skipped(
                    "Extended special slots",
                    "Disabled in settings; vanilla three-slot layout remains unchanged.")
        };

        if (settings.LoadWaylandTrader && settings.LoadItems)
        {
            var waylandSettings = await settingsLoader.LoadWaylandAsync(paths.WaylandConfigFile);
            results.Add(waylandTraderService.Load(
                paths,
                waylandSettings,
                WttContentLoader.ContentBackportDependentItemIds));
        }
        else
        {
            results.Add(ModuleResult.Skipped(
                "Wayland trader",
                settings.LoadWaylandTrader
                    ? "Disabled because custom items are disabled."
                    : "Disabled in settings."));
        }

        StartupReporter.Report(logger, results, settings);

        _loaded = true;
    }
}
