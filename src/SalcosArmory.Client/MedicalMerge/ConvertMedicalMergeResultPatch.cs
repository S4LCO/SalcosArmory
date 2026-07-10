using System.Reflection;
using SPT.Reflection.Patching;

namespace SalcosArmory.Client.MedicalMerge;

internal sealed class ConvertMedicalMergeResultPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(TraderControllerClass).GetMethod(
            nameof(TraderControllerClass.ConvertOperationResultToOperation)
        );
    }

    [PatchPrefix]
    private static bool Prefix(
        TraderControllerClass __instance,
        IRaiseEvents operationResult,
        ref BaseInventoryOperationClass __result)
    {
        if (operationResult is not MedicalMergeResult mergeResult)
        {
            return true;
        }

        __result = new MedicalMergeOperation(__instance.method_12(), __instance, mergeResult);
        return false;
    }
}
