using System.Threading.Tasks;
using Comfort.Common;
using EFT.InventoryLogic;

namespace SalcosArmory.Client.MedicalMerge;

internal sealed class MedicalMergeOperation : GClass3475<MedicalMergeResult>
{
    private readonly Item _sourceItem;
    private readonly ItemAddress _sourceAddress;
    private readonly Item _targetItem;
    private readonly ItemAddress _targetAddress;
    private readonly float _transferAmount;

    public MedicalMergeOperation(
        ushort id,
        TraderControllerClass controller,
        MedicalMergeResult result)
        : base(id, controller, result)
    {
        _sourceItem = result.Item;
        _sourceAddress = _sourceItem.Parent;
        _targetItem = result.TargetItem;
        _targetAddress = _targetItem.Parent;
        _transferAmount = result.TransferAmount;
    }

    public override async Task<IResult> ExecuteInternal()
    {
        await method_3(_sourceItem, _sourceAddress, _targetAddress, new GClass3397(_sourceItem, this));
        Execute();
        await method_4(_targetItem, _targetAddress, new GClass3398(_targetItem, _targetAddress, this));
        return method_5();
    }

    public override GClass3471 ToBaseInventoryCommand(string ownerId)
    {
        return Gstruct156_0.Value.ToCommand();
    }

    public override BaseDescriptorClass ToDescriptor()
    {
        return new MedicalMergeDescriptor
        {
            Operation = this,
            OwnerId = OwnerId,
            OperationId = Id,
            SourceItem = _sourceItem.Id,
            TargetItem = _targetItem.Id,
            TransferAmount = _transferAmount
        };
    }

    public override string ToString()
    {
        return $"Medical merge: {_sourceItem.ToFullString()} -> {_targetItem.ToFullString()}, amount={_transferAmount}";
    }
}
