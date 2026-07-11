namespace SalcosArmory.Config;

public sealed class CountermeasureProtocolSettings
{
    public int HistorySize { get; set; } = 5;
    public int MinimumRaids { get; set; } = 3;
    public double HistoryDecay { get; set; } = 0.75d;

    public double MinimumAffectedPercent { get; set; } = 25d;
    public double MaximumAffectedPercent { get; set; } = 35d;
    public int MaximumCountermeasuresPerBot { get; set; } = 2;

    public int HeavyArmorClassThreshold { get; set; } = 4;
    public double NightRaidThreshold { get; set; } = 0.5d;
    public double HeadshotRatioThreshold { get; set; } = 0.45d;
    public double LongRangeDistanceThreshold { get; set; } = 80d;
    public double SuppressorUsageThreshold { get; set; } = 0.5d;
    public double HeavyArmorUsageThreshold { get; set; } = 0.5d;
    public double SurvivalRateThreshold { get; set; } = 0.6d;

    public int MaximumAttachmentDepth { get; set; } = 3;
    public int AmmoPenetrationIncrease { get; set; } = 8;
    public int AmmoPenetrationCap { get; set; } = 50;

    public bool EnableNightVision { get; set; } = true;
    public bool EnableFaceProtection { get; set; } = true;
    public bool EnableLongRangeOptics { get; set; } = true;
    public bool EnableHearingProtection { get; set; } = true;
    public bool EnableArmorPiercingAmmo { get; set; } = true;

    public bool DebugLogging { get; set; } = false;

    public static CountermeasureProtocolSettings Default { get; } = new();
}
