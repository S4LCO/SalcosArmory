using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace SalcosArmory.Metadata;

public sealed record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = ArmoryInfo.Guid;
    public override string Name { get; init; } = ArmoryInfo.DisplayName;
    public override string Author { get; init; } = "Salco";
    public override string License { get; init; } = "MIT";

    public override Version Version { get; init; } = new(ArmoryInfo.Version);
    public override Range SptVersion { get; init; } = new("~4.0.13");

    public override string? Url { get; init; } = null;
    public override List<string>? Contributors { get; init; } = null;
    public override List<string>? Incompatibilities { get; init; } = null;

    public override Dictionary<string, Range>? ModDependencies { get; init; } = new()
    {
        ["com.wtt.commonlib"] = new Range("~2.0.20"),
        ["com.wtt.contentbackport"] = new Range("~1.0.7")
    };

    public override bool? IsBundleMod { get; init; } = false;
}
