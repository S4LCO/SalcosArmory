using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Json.Converters;

namespace SalcosArmory.MedicalMerge;

[Injectable(InjectionType.Singleton)]
public sealed class MedicalMergeRegistration(JsonUtil jsonUtil)
{
    public ModuleResult Register()
    {
        BaseInteractionRequestDataConverter.RegisterModDataHandler(
            MedicalMergeRequest.ActionName,
            jsonUtil.Deserialize<MedicalMergeRequest>
        );

        return ModuleResult.Ok(
            "Medical merge",
            $"Registered item-event action '{MedicalMergeRequest.ActionName}'."
        );
    }
}
