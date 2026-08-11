using Diz.LanguageExtensions;
using EFT;
using EFT.InventoryLogic;

namespace SalcosArmory.Client.MedicalMerge;

internal sealed class MedicalMergeResult : ISyncOperationResult, ITransferOrMergeResult
{
    private readonly Meds _source;
    private readonly Meds _target;
    private readonly OperationResult<RemoveResult> _discard;

    public MedicalMergeResult(
        Meds source,
        ItemAddress from,
        Meds target,
        float transferAmount,
        OperationResult<RemoveResult> discard,
        ItemController itemController)
    {
        _source = source;
        _target = target;
        From = from;
        TransferAmount = transferAmount;
        _discard = discard;
        ItemController = itemController;
    }

    public Item Item => _source;
    public Item ResultItem => _target;
    public ItemAddress From { get; }
    public Item TargetItem => _target;
    public float TransferAmount { get; }
    public ItemController ItemController { get; }

    public bool CanExecute(ItemController itemController)
    {
        return _source != null
            && _target != null
            && TransferAmount > 0f
            && _source.Id != _target.Id
            && _source.TemplateId == _target.TemplateId;
    }

    public OperationResult Execute()
    {
        return MedicalMergeInteraction.TryMerge(
            _source,
            _target,
            TransferAmount,
            ItemController,
            false
        );
    }

    public void RaiseEvents(IItemOwner controller, CommandStatus status)
    {
        if (_discard.Succeeded && _discard.Value != null)
        {
            _discard.Value.RaiseEvents(controller, status);
        }
        else
        {
            _source.RaiseRefreshEvent(false, true);
        }

        _target.RaiseRefreshEvent(false, true);
    }

    public void RollBack()
    {
        if (_discard.Succeeded && _discard.Value != null)
        {
            _discard.Value.RollBack();
        }

        if (TransferAmount <= 0f)
        {
            return;
        }

        var rollback = MedicalMergeInteraction.TryMerge(
            _target,
            _source,
            TransferAmount,
            ItemController,
            false
        );

        if (rollback.Failed)
        {
            SalcosArmoryPlugin.Log.LogWarning($"Medical merge rollback failed: {rollback.Error}");
        }
    }

    public MedicalMergeCommand ToCommand()
    {
        return new MedicalMergeCommand(_source.Id, _target.Id, TransferAmount);
    }
}
