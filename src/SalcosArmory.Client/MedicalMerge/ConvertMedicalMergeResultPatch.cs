using System.Reflection;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;
using SPT.Reflection.Patching;

namespace SalcosArmory.Client.MedicalMerge;

internal sealed class ConvertMedicalMergeResultPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(ItemController).GetMethod(
            nameof(ItemController.ConvertOperationResultToOperation)
        );
    }

    [PatchPrefix]
    private static bool Prefix(
        ItemController __instance,
        IOperationResult operationResult,
        ref AbstractOperation __result)
    {
        if (operationResult is not MedicalMergeResult mergeResult)
        {
            return true;
        }

        __result = new MedicalMergeOperation(
            __instance.GetAndIncrementNextOperationId(),
            __instance,
            mergeResult
        );
        return false;
    }
}
