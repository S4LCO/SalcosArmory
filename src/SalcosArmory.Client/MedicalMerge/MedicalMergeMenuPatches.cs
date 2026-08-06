using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        return AccessTools.Method(
            typeof(InventoryInteractions),
            nameof(InventoryInteractions.ExecuteInteractionInternal)
        );
    }

    [PatchPrefix]
    private static bool Prefix(InventoryInteractions __instance, EItemInfoButton interaction)
    {
        if (interaction != MedicalMergeContext.Button || !MedicalMergeContext.IsAvailable(__instance))
        {
            return true;
        }

        MedicalMergeContext.Execute(__instance);
        return false;
    }
}

internal sealed class MedicalMergeIsActivePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(
            typeof(ContextInteractions<EItemInfoButton>),
            nameof(ContextInteractions<EItemInfoButton>.IsActive)
        );
    }

    [PatchPostfix]
    private static void Postfix(
        ContextInteractions<EItemInfoButton> __instance,
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
        return AccessTools.Method(
            typeof(ContextInteractions<EItemInfoButton>),
            nameof(ContextInteractions<EItemInfoButton>.IsInteractive)
        );
    }

    [PatchPostfix]
    private static void Postfix(
        ContextInteractions<EItemInfoButton> __instance,
        EItemInfoButton button,
        ref object __result)
    {
        if (button == MedicalMergeContext.Button
            && __instance is InventoryInteractions interactions
            && MedicalMergeContext.IsAvailable(interactions))
        {
            __result = GetEnabledInteractionResult(__instance, __result);
        }
    }

    private static object GetEnabledInteractionResult(
        ContextInteractions<EItemInfoButton> interactions,
        object fallback)
    {
        try
        {
            var method = AccessTools.Method(
                typeof(ContextInteractions<EItemInfoButton>),
                nameof(ContextInteractions<EItemInfoButton>.IsInteractive),
                new[] { typeof(EItemInfoButton) }
            );

            return method?.Invoke(interactions, new object[] { EItemInfoButton.Inspect }) ?? fallback;
        }
        catch (Exception ex)
        {
            MedicalMergePlugin.Log.LogWarning($"Medical merge interaction check failed: {ex.Message}");
            return fallback;
        }
    }
}
