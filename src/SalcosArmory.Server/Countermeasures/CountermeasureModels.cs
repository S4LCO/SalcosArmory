namespace SalcosArmory.Countermeasures;

public enum CountermeasureKind
{
    NightVision,
    FaceProtection,
    LongRangeOptic,
    HearingProtection,
    ArmorPiercingAmmo
}

public sealed class CountermeasureProfileState
{
    public int SchemaVersion { get; set; } = 1;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<CountermeasureRaidRecord> Raids { get; set; } = [];
}

public sealed class CountermeasureRaidRecord
{
    public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;
    public string Location { get; set; } = string.Empty;
    public bool NightRaid { get; set; }
    public bool UsedSuppressor { get; set; }
    public bool UsedHeavyArmor { get; set; }
    public bool Survived { get; set; }
    public int Kills { get; set; }
    public int HeadshotKills { get; set; }
    public double TotalKillDistance { get; set; }
}

public sealed record CountermeasureAnalysis(
    bool IsActive,
    int RaidCount,
    double AffectedChance,
    int CountermeasuresPerBot,
    IReadOnlyList<CountermeasureKind> ActiveCountermeasures,
    double NightRaidRatio,
    double HeadshotRatio,
    double AverageKillDistance,
    double SuppressorUsageRatio,
    double HeavyArmorUsageRatio,
    double SurvivalRate,
    double Pressure)
{
    public static CountermeasureAnalysis Inactive(int raidCount) => new(
        false,
        raidCount,
        0d,
        0,
        [],
        0d,
        0d,
        0d,
        0d,
        0d,
        0d,
        0d
    );
}

public sealed record CountermeasureApplicationReport(
    bool Skipped,
    bool Selected,
    int Attempted,
    int Applied,
    IReadOnlyList<CountermeasureKind> AppliedCountermeasures)
{
    public bool HasActivity => Selected || Applied > 0;

    public static CountermeasureApplicationReport SkippedResult { get; } =
        new(true, false, 0, 0, []);
}
