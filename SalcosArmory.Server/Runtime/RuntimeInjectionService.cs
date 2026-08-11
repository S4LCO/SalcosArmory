using SalcosArmory.Config;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Runtime;

[Injectable(InjectionType.Singleton)]
public sealed class RuntimeInjectionService(
    RuntimeInjectionValidator validator,
    RuntimeInjectionPlan plan,
    RuntimeInjectionPatch patch,
    ISptLogger<RuntimeInjectionService> logger)
{
    public ModuleResult Load(RuntimeInjectionSettings settings, bool debugLogging)
    {
        if (settings.Targets is null || settings.Targets.Count == 0)
        {
            return ModuleResult.Skipped("Runtime injection", "No targets are configured.");
        }

        var validation = validator.Validate(settings);

        foreach (var warning in validation.Warnings)
        {
            logger.Warning(Log.Line(warning));
        }

        if (validation.Errors.Count > 0)
        {
            foreach (var error in validation.Errors)
            {
                logger.Error(Log.Line(error));
            }

            return ModuleResult.Failed(
                "Runtime injection",
                $"Configuration contains {validation.Errors.Count} error(s)."
            );
        }

        plan.Replace(validation.Targets);
        if (plan.IsEmpty)
        {
            return ModuleResult.Skipped("Runtime injection", "No valid targets remained after database validation.");
        }

        var enabledNow = patch.Enable(settings.ApplyToPlayerScav, debugLogging);
        var patchState = enabledNow ? "Patch enabled" : "Patch already active";

        return ModuleResult.Ok(
            "Runtime injection",
            $"{patchState}; prepared {plan.TargetCount} target(s) and {plan.SlotCount} slot mapping(s)."
        );
    }
}
