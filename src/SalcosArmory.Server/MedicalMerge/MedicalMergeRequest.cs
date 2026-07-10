using System.Text.Json.Serialization;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Request;

namespace SalcosArmory.MedicalMerge;

public sealed record MedicalMergeRequest : BaseInteractionRequestData
{
    public const string ActionName = "SalcosArmory_MergeMedical";

    [JsonPropertyName("sourceItem")]
    public MongoId? SourceItem { get; init; }

    [JsonPropertyName("targetItem")]
    public MongoId? TargetItem { get; init; }

    [JsonPropertyName("transferAmount")]
    public float TransferAmount { get; init; }
}
