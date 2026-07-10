using EFT.InventoryLogic;

namespace SalcosArmory.Client.MedicalMerge;

internal sealed class MedicalMergeResult : IExecute, IRaiseEvents, GInterface424, GInterface429, GInterface433
{
    private readonly MedsItemClass _source;
    private readonly MedsItemClass _target;
    private readonly GStruct154<GClass3408> _discard;

    public MedicalMergeResult(
        MedsItemClass source,
        ItemAddress from,
        MedsItemClass target,
        float transferAmount,
        GStruct154<GClass3408> discard,
        TraderControllerClass itemController)
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
    public TraderControllerClass ItemController { get; }

    public bool CanExecute(TraderControllerClass itemController)
    {
        return _source != null
            && _target != null
            && TransferAmount > 0f
            && _source.Id != _target.Id
            && _source.TemplateId == _target.TemplateId;
    }

    public GStruct153 Execute()
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
            MedicalMergePlugin.Log.LogWarning($"Medical merge rollback failed: {rollback.Error}");
        }
    }

    public MedicalMergeCommand ToCommand()
    {
        return new MedicalMergeCommand(_source.Id, _target.Id, TransferAmount);
    }
}
