using System.Collections.ObjectModel;

namespace SalcosArmory.Configurator.Models;

public sealed class ConfigWorkspace
{
    public string ConfigDirectory { get; set; } = string.Empty;
    public GeneralConfig General { get; set; } = new();
    public CountermeasureConfig Countermeasures { get; set; } = new();
    public WaylandConfig Wayland { get; set; } = new();
    public SoftArmorConfig SoftArmor { get; set; } = new();
    public RuntimeInjectionConfig RuntimeInjection { get; set; } = new();
    public ObservableCollection<AdvancedConfigFile> AdvancedFiles { get; set; } = [];
}

public sealed class GeneralConfig
{
    public bool LoadItems { get; set; } = true;
    public bool LoadWeaponPresets { get; set; } = true;
    public bool LoadHideoutRecipes { get; set; } = true;
    public bool LoadLocales { get; set; } = true;
    public bool LoadBuffs { get; set; } = true;
    public bool LoadCompat { get; set; } = true;
    public bool LoadRuntimeInjection { get; set; } = true;
    public bool LoadMedicalMerge { get; set; } = true;
    public bool LoadCountermeasureProtocol { get; set; } = true;
    public bool LoadWaylandTrader { get; set; } = true;
    public bool LoadExtendedSpecialSlots { get; set; } = true;
    public bool Debug { get; set; }
    public bool StrictMode { get; set; }
}

public sealed class CountermeasureConfig
{
    public int HistorySize { get; set; } = 5;
    public int MinimumRaids { get; set; } = 3;
    public double HistoryDecay { get; set; } = 0.75;
    public double MinimumAffectedPercent { get; set; } = 25;
    public double MaximumAffectedPercent { get; set; } = 35;
    public int MaximumCountermeasuresPerBot { get; set; } = 2;
    public int HeavyArmorClassThreshold { get; set; } = 4;
    public double NightRaidThreshold { get; set; } = 0.50;
    public double HeadshotRatioThreshold { get; set; } = 0.45;
    public double LongRangeDistanceThreshold { get; set; } = 80;
    public double SuppressorUsageThreshold { get; set; } = 0.50;
    public double HeavyArmorUsageThreshold { get; set; } = 0.50;
    public double SurvivalRateThreshold { get; set; } = 0.60;
    public int MaximumAttachmentDepth { get; set; } = 3;
    public int AmmoPenetrationIncrease { get; set; } = 8;
    public int AmmoPenetrationCap { get; set; } = 50;
    public bool EnableNightVision { get; set; } = true;
    public bool EnableFaceProtection { get; set; } = true;
    public bool EnableLongRangeOptics { get; set; } = true;
    public bool EnableHearingProtection { get; set; } = true;
    public bool EnableArmorPiercingAmmo { get; set; } = true;
    public bool DebugLogging { get; set; }
}

public sealed class WaylandConfig
{
    public bool ShowOffersOnFlea { get; set; } = true;
    public string PriceSource { get; set; } = "flea";
    public double PriceMultiplier { get; set; } = 1.0;
    public int MinimumPrice { get; set; } = 1;
    public int RefreshTimeMinMinutes { get; set; } = 60;
    public int RefreshTimeMaxMinutes { get; set; } = 120;
    public ObservableCollection<WaylandCategoryRow> Categories { get; set; } = [];
    public ObservableCollection<WaylandItemRow> ItemOverrides { get; set; } = [];
}

public sealed class WaylandCategoryRow
{
    public string Name { get; set; } = string.Empty;
    public int LoyaltyLevel { get; set; } = 1;
    public int Stock { get; set; } = 3;
}

public sealed class WaylandItemRow
{
    public string TemplateId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int? LoyaltyLevel { get; set; }
    public int? Stock { get; set; }
    public int? Price { get; set; }
}

public sealed class SoftArmorConfig
{
    public bool Enabled { get; set; } = true;
    public ObservableCollection<SoftArmorClassRow> Classes { get; set; } = [];
    public ObservableCollection<SoftArmorPositionRow> Positions { get; set; } = [];
}

public sealed class SoftArmorClassRow
{
    public string ArmorClass { get; set; } = string.Empty;
    public int BaseDurability { get; set; }
    public double BluntThroughput { get; set; }
    public double FullSetWeightKg { get; set; }
    public double FullSetSpeedPenaltyPercent { get; set; }
    public double FullSetMousePenalty { get; set; }
    public double FullSetErgonomicPenalty { get; set; }
    public int RepairCost { get; set; }
    public int FrontBackFleaPrice { get; set; }
    public int FrontBackHandbookPrice { get; set; }
    public int StaticLootWeight { get; set; }
    public int WaylandStock { get; set; }
}

public sealed class SoftArmorPositionRow
{
    public string Position { get; set; } = string.Empty;
    public double DurabilityMultiplier { get; set; } = 1;
    public double SetShare { get; set; } = 1;
    public double PriceMultiplier { get; set; } = 1;
}

public sealed class RuntimeInjectionConfig
{
    public bool ApplyToPlayerScav { get; set; } = true;
    public ObservableCollection<RuntimeTargetRow> Targets { get; set; } = [];
}

public sealed class RuntimeTargetRow
{
    public string ItemTpl { get; set; } = string.Empty;
    public ObservableCollection<RuntimeSlotRow> Slots { get; set; } = [];
}

public sealed class RuntimeSlotRow
{
    public string SlotName { get; set; } = string.Empty;
    public string ItemTpl { get; set; } = string.Empty;
}

public sealed class AdvancedConfigFile
{
    public string RelativePath { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public enum ConfigSection
{
    General,
    Countermeasures,
    Wayland,
    SoftArmor,
    RuntimeInjection
}
