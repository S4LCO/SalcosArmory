using SPTarkov.DI.Annotations;

namespace SalcosArmory.MedicalMerge;

[Injectable(InjectionType.Singleton)]
public sealed class MedicalMergeFeatureState
{
    public bool Enabled { get; set; }
}

[Injectable(InjectionType.Singleton)]
public sealed class MedicalMergeRegistration(MedicalMergeFeatureState state)
{
    public ModuleResult Register()
    {
        state.Enabled = true;

        return ModuleResult.Ok(
            "Medical merge",
            $"Enabled item-event action '{MedicalMergeRequest.ActionName}'."
        );
    }
}
