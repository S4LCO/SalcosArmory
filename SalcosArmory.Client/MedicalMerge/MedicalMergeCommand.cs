using System;
using EFT.InventoryLogic.Operations;
using Newtonsoft.Json;

namespace SalcosArmory.Client.MedicalMerge;

[Serializable]
internal sealed class MedicalMergeCommand : BaseInventoryCommand
{
    public const string ActionName = "SalcosArmory_MergeMedical";

    public string Action = ActionName;

    [JsonProperty("sourceItem")]
    public string SourceItem;

    [JsonProperty("targetItem")]
    public string TargetItem;

    [JsonProperty("transferAmount")]
    public float TransferAmount;

    public MedicalMergeCommand(string sourceItem, string targetItem, float transferAmount)
    {
        SourceItem = sourceItem;
        TargetItem = targetItem;
        TransferAmount = transferAmount;
    }

    public override bool Queued => false;
}
