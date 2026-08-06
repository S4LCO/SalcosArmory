using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using EFT.InventoryLogic.Operations;

namespace SalcosArmory.Client.MedicalMerge;

internal sealed class MedicalMergeOperation : AbstractAsyncOperation<MedicalMergeResult>
{
    private readonly Item _sourceItem;
    private readonly ItemAddress _sourceAddress;
    private readonly Item _targetItem;
    private readonly ItemAddress _targetAddress;
    private readonly float _transferAmount;

    public MedicalMergeOperation(
        ushort id,
        ItemController controller,
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
        await OutProcess(
            _sourceItem,
            _sourceAddress,
            _targetAddress,
            new AddSuboperation(_sourceItem, this)
        );
        Execute();
        await InProcess(
            _targetItem,
            _targetAddress,
            new RemoveSuboperation(_targetItem, _targetAddress, this)
        );
        return FinishExecution();
    }

    public override BaseInventoryCommand ToBaseInventoryCommand(string ownerId)
    {
        return new MedicalMergeCommand(_sourceItem.Id, _targetItem.Id, _transferAmount);
    }

    public override InventoryOperationDescriptor ToDescriptor()
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
