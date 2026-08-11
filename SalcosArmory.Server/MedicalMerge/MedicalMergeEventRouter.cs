using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI.Routing;

namespace SalcosArmory.MedicalMerge;

[Injectable(InjectionType.Singleton)]
public sealed class MedicalMergeEventRouter(MedicalMergeService medicalMergeService)
    : ItemEventRouter([
        new ItemRouteAction<MedicalMergeRequest>(
            MedicalMergeRequest.ActionName,
            (url, pmcData, body, sessionId, output, cancellationToken) =>
                medicalMergeService.Merge(pmcData, body, sessionId, output))
    ]);
