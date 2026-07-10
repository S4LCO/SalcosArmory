using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Runtime;

[Injectable(InjectionType.Singleton)]
public sealed class RuntimeInjectionPatch(
    RuntimeItemInjector injector,
    ISptLogger<RuntimeInjectionPatch> logger)
    : AbstractPatch(nameof(RuntimeInjectionPatch))
{
    private static bool _enabled;

    public bool Enable(bool includePlayerScav, bool debugLogging)
    {
        PatchState.Injector = injector;
        PatchState.Logger = logger;
        PatchState.IncludePlayerScav = includePlayerScav;
        PatchState.DebugLogging = debugLogging;

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
    private static void Postfix(BotBaseInventory __result, BotGenerationDetails botGenerationDetails)
    {
        if (PatchState.Injector is null)
        {
            return;
        }

        try
        {
            var report = PatchState.Injector.Apply(
                __result,
                PatchState.IncludePlayerScav,
                botGenerationDetails.IsPlayerScav
            );

            if (PatchState.DebugLogging && report.HasActivity)
            {
                PatchState.Logger?.Info(Log.Line(
                    $"Runtime injection: matched={report.HostsMatched}, considered={report.SlotsConsidered}, " +
                    $"added={report.InsertsAdded}, occupied={report.OccupiedSlots}, missingSlots={report.MissingSlots}, " +
                    $"missingTemplates={report.MissingTemplates}, blocked={report.BlockedInserts}."
                ));
            }
        }
        catch (Exception ex)
        {
            PatchState.Logger?.Error(Log.Line($"Runtime injection failed for a generated inventory: {ex.Message}"));
        }
    }

    private static class PatchState
    {
        public static RuntimeItemInjector? Injector { get; set; }
        public static ISptLogger<RuntimeInjectionPatch>? Logger { get; set; }
        public static bool IncludePlayerScav { get; set; }
        public static bool DebugLogging { get; set; }
    }
}
