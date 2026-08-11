using SalcosArmory.Config;
using SalcosArmory.Content;
using SalcosArmory.Traders;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Mod;

/// <summary>
/// Completes content that depends on other WTT mods at the end of SPT's preload window.
/// Content Backport has registered its templates by this point, while the resulting SALCO
/// templates are still available before core callbacks and profile processing begin.
/// </summary>
[Injectable(InjectionType.Singleton, TypePriority = OnLoadOrder.GameCallbacks - 1)]
public sealed class SalcosArmoryDependencyLoad(
    SettingsLoader settingsLoader,
    SalcosArmoryPreload preload,
    WttContentLoader contentLoader,
    WaylandTraderService waylandTraderService,
    ISptLogger<SalcosArmoryDependencyLoad> logger
) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!preload.IsLoaded)
        {
            await preload.OnLoadAsync(cancellationToken);
        }

        var paths = preload.Paths
            ?? throw new InvalidOperationException("SALCO's ARMORY preload did not initialize its paths.");
        var settings = preload.Settings
            ?? throw new InvalidOperationException("SALCO's ARMORY preload did not initialize its settings.");

        var contentResult = await contentLoader.LoadDeferredContentBackportAsync(
            Assembly.GetExecutingAssembly(),
            paths,
            settings);
        LogResult(contentResult, settings);

        if (contentResult.Success
            && !contentResult.IsSkipped
            && settings.LoadWaylandTrader
            && settings.LoadItems)
        {
            var waylandSettings = await settingsLoader.LoadWaylandAsync(paths.WaylandConfigFile);
            var waylandResult = waylandTraderService.Load(paths, waylandSettings);
            LogResult(waylandResult, settings);
        }
    }

    private void LogResult(ModuleResult result, ArmorySettings settings)
    {
        var state = result.Success ? result.IsSkipped ? "SKIP" : "OK" : "FAIL";
        logger.Info(Log.Line($"{result.Name}: {state} - {result.Message}"));

        if (!result.Success && settings.StrictMode)
        {
            throw new InvalidOperationException(
                $"{ArmoryInfo.DisplayName} stopped during {result.Name}: {result.Message}");
        }
    }
}
