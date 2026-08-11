using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using InventoryInteractions = EFT.UI.InventoryItemContextInteractions;

namespace SalcosArmory.Client.MedicalMerge;

internal sealed class MedicalMergeAvailableInteractionsPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.PropertyGetter(
            typeof(InventoryInteractions),
            nameof(InventoryInteractions.AvailableInteractions)
        );
    }

    [PatchPostfix]
    private static void Postfix(
        InventoryInteractions __instance,
        ref IEnumerable<EItemInfoButton> __result)
    {
        var interactions = (__result ?? Enumerable.Empty<EItemInfoButton>()).Distinct().ToList();

        if (MedicalMergeContext.IsAvailable(__instance))
        {
            interactions.RemoveAll(button => button == MedicalMergeContext.Button);
            interactions.Add(MedicalMergeContext.Button);
        }

        __result = interactions;
    }
}

internal sealed class MedicalMergeLabelPatch : ModulePatch
{
    private static string _originalTopUpLabel;

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(ItemUiContext), nameof(ItemUiContext.ShowContextMenu));
    }

    [PatchPrefix]
    private static void Prefix(
        ItemUiContext __instance,
        ItemContext itemContext,
        Dictionary<EItemInfoButton, string> ____contextMenuCustomNames)
    {
        if (____contextMenuCustomNames == null)
        {
            return;
        }

        if (_originalTopUpLabel == null
            && ____contextMenuCustomNames.TryGetValue(EItemInfoButton.TopUp, out var originalLabel))
        {
            _originalTopUpLabel = originalLabel == MedicalMergeContext.Label
                ? EItemInfoButton.TopUp.ToString()
                : originalLabel;
        }

        var useMergeLabel = itemContext?.Item is Meds medicalItem
            && MedicalMergeContext.IsAvailable(__instance, medicalItem);

        if (useMergeLabel)
        {
            ____contextMenuCustomNames[EItemInfoButton.TopUp] = MedicalMergeContext.Label;
        }
        else if (_originalTopUpLabel != null)
        {
            ____contextMenuCustomNames[EItemInfoButton.TopUp] = _originalTopUpLabel;
        }
    }

    [PatchPostfix]
    private static void Postfix(Dictionary<EItemInfoButton, string> ____contextMenuCustomNames)
    {
        if (____contextMenuCustomNames != null && _originalTopUpLabel != null)
        {
            ____contextMenuCustomNames[EItemInfoButton.TopUp] = _originalTopUpLabel;
        }
    }
}

internal sealed class MedicalMergeExecutePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.DeclaredMethod(
            typeof(BaseInventoryItemContextInteractions),
            nameof(BaseInventoryItemContextInteractions.ExecuteInteractionInternal),
            new[] { typeof(EItemInfoButton) }
        );
    }

    [PatchPrefix]
    private static bool Prefix(
        BaseInventoryItemContextInteractions __instance,
        EItemInfoButton interaction)
    {
        if (interaction != MedicalMergeContext.Button
            || __instance is not InventoryInteractions interactions
            || !MedicalMergeContext.IsAvailable(interactions))
        {
            return true;
        }

        MedicalMergeContext.Execute(interactions);
        return false;
    }
}

internal sealed class MedicalMergeIsActivePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.DeclaredMethod(
            typeof(BaseItemContextInteractions),
            nameof(BaseItemContextInteractions.IsActive),
            new[] { typeof(EItemInfoButton) }
        );
    }

    [PatchPostfix]
    private static void Postfix(
        BaseItemContextInteractions __instance,
        EItemInfoButton button,
        ref bool __result)
    {
        if (button == MedicalMergeContext.Button
            && __instance is InventoryInteractions interactions
            && MedicalMergeContext.IsAvailable(interactions))
        {
            __result = true;
        }
    }
}

internal sealed class MedicalMergeIsInteractivePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.DeclaredMethod(
            typeof(BaseItemContextInteractions),
            nameof(BaseItemContextInteractions.IsInteractive),
            new[] { typeof(EItemInfoButton) }
        );
    }

    [PatchPostfix]
    private static void Postfix(
        BaseItemContextInteractions __instance,
        EItemInfoButton button,
        ref IResult __result)
    {
        if (button == MedicalMergeContext.Button
            && __instance is InventoryInteractions interactions
            && MedicalMergeContext.IsAvailable(interactions))
        {
            __result = GetEnabledInteractionResult(__instance, __result);
        }
    }

    private static IResult GetEnabledInteractionResult(
        BaseItemContextInteractions interactions,
        IResult fallback)
    {
        try
        {
            return interactions.IsInteractive(EItemInfoButton.Inspect) ?? fallback;
        }
        catch (Exception ex)
        {
            MedicalMergePlugin.Log.LogWarning($"Medical merge interaction check failed: {ex.Message}");
            return fallback;
        }
    }
}
