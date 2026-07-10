using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Request;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;

namespace SalcosArmory.MedicalMerge;

[Injectable(InjectionType.Singleton)]
public sealed class MedicalMergeEventRouter(MedicalMergeService medicalMergeService) : ItemEventRouterDefinition
{
    public override ValueTask<ItemEventRouterResponse> HandleItemEvent(
        string url,
        PmcData pmcData,
        BaseInteractionRequestData body,
        MongoId sessionID,
        ItemEventRouterResponse output)
    {
        return Route(url, pmcData, body, sessionID);
    }

    protected override ValueTask<ItemEventRouterResponse> HandleItemEventInternal(
        string url,
        PmcData pmcData,
        BaseInteractionRequestData body,
        MongoId sessionID,
        ItemEventRouterResponse output)
    {
        return Route(url, pmcData, body, sessionID);
    }

    protected override List<HandledRoute> GetHandledRoutes()
    {
        return [new HandledRoute(MedicalMergeRequest.ActionName, false)];
    }

    private ValueTask<ItemEventRouterResponse> Route(
        string url,
        PmcData pmcData,
        BaseInteractionRequestData body,
        MongoId sessionId)
    {
        if (url != MedicalMergeRequest.ActionName)
        {
            throw new InvalidOperationException($"Medical merge router cannot handle route '{url}'.");
        }

        return medicalMergeService.Merge(pmcData, body as MedicalMergeRequest, sessionId);
    }
}
