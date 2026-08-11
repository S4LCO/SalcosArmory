using SalcosArmory.Config;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Infrastructure;

internal static class StartupReporter
{
    public static void Report<TLogger>(
        ISptLogger<TLogger> logger,
        IEnumerable<ModuleResult> results,
        ArmorySettings settings)
    {
        foreach (var result in results)
        {
            Report(logger, result, settings);
        }
    }

    public static void Report<TLogger>(
        ISptLogger<TLogger> logger,
        ModuleResult result,
        ArmorySettings settings)
    {
        if (settings.Debug || !result.Success)
        {
            var state = result.Success
                ? result.IsSkipped ? "SKIP" : "OK"
                : "FAIL";
            var message = Log.Line($"{result.Name}: {state} - {result.Message}");

            if (result.Success)
            {
                logger.Info(message);
            }
            else
            {
                logger.Error(message);
            }
        }

        if (!result.Success && settings.StrictMode)
        {
            throw new InvalidOperationException(
                $"{ArmoryInfo.DisplayName} stopped during {result.Name}: {result.Message}");
        }
    }
}
