using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace SalcosArmory.Metadata;

public sealed record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = ArmoryInfo.Guid;
    public string Name { get; init; } = ArmoryInfo.DisplayName;
    public string Author { get; init; } = "Salco";
    public string License { get; init; } = "MIT";

    public Version Version { get; init; } = new(ArmoryInfo.Version);
    public Range SptVersion { get; init; } = new("~4.1.0");

    public string? Url { get; init; } = null;
    public List<string>? Contributors { get; init; } = null;
    public List<string>? Incompatibilities { get; init; } = null;

    public Dictionary<string, Range>? ModDependencies { get; init; } = new()
    {
        ["com.wtt.commonlib"] = new Range(ArmoryInfo.WttCommonLibVersionRange)
    };

    public bool HasPrepatcher { get; init; } = false;
}
