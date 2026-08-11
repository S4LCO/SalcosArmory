using SalcosArmory.Config;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Countermeasures;

[Injectable(InjectionType.Singleton)]
public sealed class CountermeasureProtocolService(
    CountermeasureStateStore stateStore,
    RaidTelemetryService telemetryService,
    CountermeasureInventoryService inventoryService,
    CountermeasureRaidConfigurationPatch raidConfigurationPatch,
    CountermeasureRaidEndPatch raidEndPatch,
    CountermeasureInventoryPatch inventoryPatch,
    ISptLogger<CountermeasureProtocolService> logger)
{
    public ModuleResult Load(
        CountermeasureProtocolSettings settings,
        ArmoryPaths paths,
        bool globalDebugLogging)
    {
        Normalize(settings);

        try
        {
            stateStore.Configure(paths.CountermeasureData, settings);
            telemetryService.Configure(settings, globalDebugLogging);
            inventoryService.Configure(settings);

            var startEnabled = raidConfigurationPatch.EnablePatch();
            var endEnabled = raidEndPatch.EnablePatch();
            var inventoryEnabled = inventoryPatch.EnablePatch(globalDebugLogging || settings.DebugLogging);

            var message =
                $"Enabled. History={settings.HistorySize}, MinimumRaids={settings.MinimumRaids}, " +
                $"Affected={settings.MinimumAffectedPercent:0.#}-{settings.MaximumAffectedPercent:0.#}%, " +
                $"MaximumMeasures={settings.MaximumCountermeasuresPerBot}. " +
                $"Patches(start/end/inventory)={startEnabled}/{endEnabled}/{inventoryEnabled}.";

            if (globalDebugLogging || settings.DebugLogging)
            {
                logger.Info(Log.Line($"Countermeasure Protocol: {message}"));
            }
            return ModuleResult.Ok("Countermeasure Protocol", message);
        }
        catch (Exception ex)
        {
            logger.Error(Log.Line($"Countermeasure Protocol could not be enabled: {ex.Message}"));
            return ModuleResult.Failed("Countermeasure Protocol", ex.Message);
        }
    }

    private static void Normalize(CountermeasureProtocolSettings settings)
    {
        settings.HistorySize = Math.Clamp(settings.HistorySize, 3, 20);
        settings.MinimumRaids = Math.Clamp(settings.MinimumRaids, 1, settings.HistorySize);
        settings.HistoryDecay = Math.Clamp(settings.HistoryDecay, 0.1d, 1d);

        settings.MinimumAffectedPercent = Math.Clamp(settings.MinimumAffectedPercent, 0d, 100d);
        settings.MaximumAffectedPercent = Math.Clamp(
            settings.MaximumAffectedPercent,
            settings.MinimumAffectedPercent,
            100d);
        settings.MaximumCountermeasuresPerBot = Math.Clamp(settings.MaximumCountermeasuresPerBot, 1, 5);

        settings.HeavyArmorClassThreshold = Math.Clamp(settings.HeavyArmorClassThreshold, 1, 6);
        settings.NightRaidThreshold = Math.Clamp(settings.NightRaidThreshold, 0d, 1d);
        settings.HeadshotRatioThreshold = Math.Clamp(settings.HeadshotRatioThreshold, 0d, 1d);
        settings.LongRangeDistanceThreshold = Math.Clamp(settings.LongRangeDistanceThreshold, 1d, 500d);
        settings.SuppressorUsageThreshold = Math.Clamp(settings.SuppressorUsageThreshold, 0d, 1d);
        settings.HeavyArmorUsageThreshold = Math.Clamp(settings.HeavyArmorUsageThreshold, 0d, 1d);
        settings.SurvivalRateThreshold = Math.Clamp(settings.SurvivalRateThreshold, 0d, 1d);

        settings.MaximumAttachmentDepth = Math.Clamp(settings.MaximumAttachmentDepth, 1, 4);
        settings.AmmoPenetrationIncrease = Math.Clamp(settings.AmmoPenetrationIncrease, 0, 30);
        settings.AmmoPenetrationCap = Math.Clamp(settings.AmmoPenetrationCap, 0, 100);
    }
}
