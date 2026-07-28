namespace SalcosArmory.Config;

public sealed class ArmorySettings
{
    public bool LoadItems { get; init; } = true;
    public bool LoadWeaponPresets { get; init; } = true;
    public bool LoadHideoutRecipes { get; init; } = true;
    public bool LoadLocales { get; init; } = true;
    public bool LoadBuffs { get; init; } = true;
    public bool LoadCompat { get; init; } = true;
    public bool LoadRuntimeInjection { get; init; } = false;
    public bool LoadMedicalMerge { get; init; } = true;
    public bool LoadCountermeasureProtocol { get; init; } = true;
    public bool LoadWaylandTrader { get; init; } = true;
    public bool Debug { get; init; } = false;
    public bool StrictMode { get; init; } = false;

    public static ArmorySettings Default { get; } = new();
}
