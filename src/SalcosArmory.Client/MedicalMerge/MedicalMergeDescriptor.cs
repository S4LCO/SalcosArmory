using EFT;

namespace SalcosArmory.Client.MedicalMerge;

public sealed class MedicalMergeDescriptor : BaseDescriptorClass
{
    public string SourceItem = string.Empty;
    public string TargetItem = string.Empty;
    public float TransferAmount;

    public override GStruct152<BaseInventoryOperationClass> ToInventoryOperation(IPlayer player)
    {
        var sourceResult = player.FindItemById(SourceItem);
        if (sourceResult.Failed)
        {
            return sourceResult.Error;
        }

        if (sourceResult.Value is not MedsItemClass source)
        {
            return new WrongItemTypeError(sourceResult.Value);
        }

        var targetResult = player.FindItemById(TargetItem);
        if (targetResult.Failed)
        {
            return targetResult.Error;
        }

        if (targetResult.Value is not MedsItemClass target)
        {
            return new WrongItemTypeError(targetResult.Value);
        }

        var mergeResult = MedicalMergeInteraction.TryMerge(
            source,
            target,
            TransferAmount,
            player.InventoryController,
            true
        );

        if (mergeResult.Failed)
        {
            return mergeResult.Error;
        }

        return new MedicalMergeOperation(OperationId, player.InventoryController, mergeResult.Value);
    }

    public override string ToString()
    {
        return $"Medical merge: {SourceItem} -> {TargetItem}, amount={TransferAmount}";
    }
}

public sealed class WrongItemTypeError : GClass1522
{
    public WrongItemTypeError(EFT.InventoryLogic.Item item)
        : base($"SALCO's ARMORY medical merge failed: wrong item type '{item?.TemplateId}'.")
    {
    }
}
