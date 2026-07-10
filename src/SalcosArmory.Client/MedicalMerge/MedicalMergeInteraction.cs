using EFT.InventoryLogic;
using UnityEngine;

namespace SalcosArmory.Client.MedicalMerge;

internal static class MedicalMergeInteraction
{
    private const float ResourceEpsilon = 0.001f;

    public static bool CanMerge(
        MedsItemClass source,
        MedsItemClass target,
        out MedicalMergeBlockReason reason)
    {
        reason = MedicalMergeBlockReason.None;

        if (source == null || target == null)
        {
            reason = MedicalMergeBlockReason.MissingItem;
        }
        else if (source.Id == target.Id)
        {
            reason = MedicalMergeBlockReason.SameItem;
        }
        else if (source.TemplateId != target.TemplateId)
        {
            reason = MedicalMergeBlockReason.DifferentTemplate;
        }
        else if (source.MedKitComponent == null || target.MedKitComponent == null)
        {
            reason = MedicalMergeBlockReason.MissingMedicalResource;
        }
        else if (source.MedKitComponent.HpResource <= ResourceEpsilon)
        {
            reason = MedicalMergeBlockReason.SourceEmpty;
        }
        else if (target.MedKitComponent.HpResource >= target.MedKitComponent.MaxHpResource - ResourceEpsilon)
        {
            reason = MedicalMergeBlockReason.TargetFull;
        }
        else if (GetTransferableAmount(source, target) <= ResourceEpsilon)
        {
            reason = MedicalMergeBlockReason.NothingToTransfer;
        }

        return reason == MedicalMergeBlockReason.None;
    }

    public static GStruct154<MedicalMergeResult> TryMerge(
        MedsItemClass source,
        MedsItemClass target,
        float requestedAmount,
        TraderControllerClass itemController,
        bool simulate)
    {
        if (!CanMerge(source, target, out var reason))
        {
            if (reason.IsHarmlessStaleState())
            {
                return new MedicalMergeResult(source, source.CurrentAddress, target, 0f, default, itemController);
            }

            return new GClass1522($"SALCO's ARMORY medical merge failed: {reason.Describe()}.");
        }

        var sourceResource = source.MedKitComponent;
        var targetResource = target.MedKitComponent;
        var originalSourceAmount = sourceResource.HpResource;
        var originalTargetAmount = targetResource.HpResource;
        var availableAmount = GetTransferableAmount(source, target);
        var transferAmount = requestedAmount > 0f
            ? Mathf.Min(requestedAmount, availableAmount)
            : availableAmount;

        if (transferAmount <= ResourceEpsilon)
        {
            return new MedicalMergeResult(source, source.CurrentAddress, target, 0f, default, itemController);
        }

        var sourceIsDepleted = transferAmount >= originalSourceAmount - ResourceEpsilon;
        sourceResource.HpResource = sourceIsDepleted ? 0f : originalSourceAmount - transferAmount;
        targetResource.HpResource = originalTargetAmount + transferAmount;

        GStruct154<GClass3408> discard = default;
        if (sourceIsDepleted)
        {
            discard = InteractionsHandlerClass.Discard(source, itemController, false);
            if (!discard.Succeeded)
            {
                sourceResource.HpResource = originalSourceAmount;
                targetResource.HpResource = originalTargetAmount;
                return discard.Error;
            }
        }

        if (simulate)
        {
            discard.Value?.RollBack();
            sourceResource.HpResource = originalSourceAmount;
            targetResource.HpResource = originalTargetAmount;
        }

        return new MedicalMergeResult(
            source,
            source.CurrentAddress,
            target,
            transferAmount,
            discard,
            itemController
        );
    }

    private static float GetTransferableAmount(MedsItemClass source, MedsItemClass target)
    {
        return Mathf.Min(
            source.MedKitComponent.HpResource,
            target.MedKitComponent.MaxHpResource - target.MedKitComponent.HpResource
        );
    }
}

internal enum MedicalMergeBlockReason
{
    None,
    MissingItem,
    SameItem,
    DifferentTemplate,
    MissingMedicalResource,
    SourceEmpty,
    TargetFull,
    NothingToTransfer
}

internal static class MedicalMergeBlockReasonExtensions
{
    public static bool IsHarmlessStaleState(this MedicalMergeBlockReason reason)
    {
        return reason == MedicalMergeBlockReason.SourceEmpty
            || reason == MedicalMergeBlockReason.TargetFull
            || reason == MedicalMergeBlockReason.NothingToTransfer;
    }

    public static string Describe(this MedicalMergeBlockReason reason)
    {
        switch (reason)
        {
            case MedicalMergeBlockReason.MissingItem:
                return "source or target is missing";
            case MedicalMergeBlockReason.SameItem:
                return "source and target are the same item";
            case MedicalMergeBlockReason.DifferentTemplate:
                return "items use different templates";
            case MedicalMergeBlockReason.MissingMedicalResource:
                return "an item has no medical resource";
            case MedicalMergeBlockReason.SourceEmpty:
                return "source is empty";
            case MedicalMergeBlockReason.TargetFull:
                return "target is already full";
            case MedicalMergeBlockReason.NothingToTransfer:
                return "nothing can be transferred";
            default:
                return "unknown reason";
        }
    }
}
