using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Countermeasures;

[Injectable(InjectionType.Singleton)]
public sealed class CountermeasureInventoryPatch(
    CountermeasureInventoryService inventoryService,
    ISptLogger<CountermeasureInventoryPatch> logger)
    : AbstractPatch(nameof(CountermeasureInventoryPatch))
{
    private static bool _enabled;
    private static bool _debugLogging;
    private static CountermeasureInventoryService? _inventoryService;
    private static ISptLogger<CountermeasureInventoryPatch>? _logger;

    public bool EnablePatch(bool debugLogging)
    {
        _inventoryService = inventoryService;
        _logger = logger;
        _debugLogging = debugLogging;

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
        return typeof(BotInventoryGenerator).GetMethod(
            "GenerateInventory",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            [typeof(MongoId), typeof(MongoId), typeof(BotType), typeof(BotGenerationDetails)],
            null
        );
    }

    [PatchPostfix]
    private static void Postfix(
        BotBaseInventory __result,
        MongoId sessionId,
        BotGenerationDetails botGenerationDetails)
    {
        if (_inventoryService is null)
        {
            return;
        }

        try
        {
            var report = _inventoryService.Apply(sessionId, __result, botGenerationDetails);
            if (_debugLogging && report.HasActivity)
            {
                _logger?.Info(Log.Line(
                    $"Countermeasure inventory: selected={report.Selected}, attempted={report.Attempted}, " +
                    $"applied={report.Applied}, measures=[{string.Join(", ", report.AppliedCountermeasures)}]."
                ));
            }
        }
        catch (Exception ex)
        {
            _logger?.Error(Log.Line($"Countermeasure inventory failed safely: {ex.Message}"));
        }
    }
}
