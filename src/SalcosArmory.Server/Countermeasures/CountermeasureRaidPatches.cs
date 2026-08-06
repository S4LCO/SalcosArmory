using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Countermeasures;

[Injectable(InjectionType.Singleton)]
public sealed class CountermeasureRaidConfigurationPatch(
    RaidTelemetryService telemetry,
    ISptLogger<CountermeasureRaidConfigurationPatch> logger)
    : AbstractPatch(nameof(CountermeasureRaidConfigurationPatch))
{
    private static bool _enabled;
    private static RaidTelemetryService? _telemetry;
    private static ISptLogger<CountermeasureRaidConfigurationPatch>? _logger;

    public bool EnablePatch()
    {
        _telemetry = telemetry;
        _logger = logger;
        if (_enabled)
        {
            return false;
        }

        base.Enable();
        _enabled = true;
        return true;
    }

    protected override MethodBase? GetTargetMethod()
    {
        return typeof(MatchController).GetMethod(
            nameof(MatchController.ConfigureOfflineRaid),
            BindingFlags.Instance | BindingFlags.Public,
            null,
            [typeof(GetRaidConfigurationRequestData), typeof(MongoId)],
            null
        );
    }

    [PatchPostfix]
    private static void Postfix(GetRaidConfigurationRequestData request, MongoId sessionId)
    {
        try
        {
            _telemetry?.CaptureRaidStart(sessionId, request);
        }
        catch (Exception ex)
        {
            _logger?.Warning(Log.Line($"Countermeasure raid-start patch failed safely: {ex.Message}"));
        }
    }
}

[Injectable(InjectionType.Singleton)]
public sealed class CountermeasureRaidEndPatch(
    RaidTelemetryService telemetry,
    ISptLogger<CountermeasureRaidEndPatch> logger)
    : AbstractPatch(nameof(CountermeasureRaidEndPatch))
{
    private static bool _enabled;
    private static RaidTelemetryService? _telemetry;
    private static ISptLogger<CountermeasureRaidEndPatch>? _logger;

    public bool EnablePatch()
    {
        _telemetry = telemetry;
        _logger = logger;
        if (_enabled)
        {
            return false;
        }

        base.Enable();
        _enabled = true;
        return true;
    }

    protected override MethodBase? GetTargetMethod()
    {
        return typeof(MatchController).GetMethod(
            nameof(MatchController.EndLocalRaidAsync),
            BindingFlags.Instance | BindingFlags.Public,
            null,
            [typeof(MongoId), typeof(EndLocalRaidRequestData), typeof(CancellationToken)],
            null
        );
    }

    [PatchPostfix]
    private static void Postfix(MongoId sessionId, EndLocalRaidRequestData request)
    {
        try
        {
            _telemetry?.CaptureRaidEnd(sessionId, request);
        }
        catch (Exception ex)
        {
            _logger?.Warning(Log.Line($"Countermeasure raid-end patch failed safely: {ex.Message}"));
        }
    }
}
