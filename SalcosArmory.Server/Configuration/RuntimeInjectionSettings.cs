namespace SalcosArmory.Config;

public sealed class RuntimeInjectionSettings
{
    public bool ApplyToPlayerScav { get; init; } = false;
    public List<RuntimeInjectionTarget> Targets { get; init; } = [];

    public static RuntimeInjectionSettings Default { get; } = new();
}

public sealed class RuntimeInjectionTarget
{
    public string ItemTpl { get; init; } = string.Empty;
    public Dictionary<string, string> Slots { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
