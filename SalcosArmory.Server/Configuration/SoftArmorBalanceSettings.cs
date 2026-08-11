namespace SalcosArmory.Config;

public sealed class SoftArmorBalanceSettings
{
    public bool Enabled { get; init; } = true;
    public Dictionary<string, SoftArmorClassBalance> Classes { get; init; } = CreateDefaultClasses();
    public Dictionary<string, SoftArmorPositionBalance> Positions { get; init; } = CreateDefaultPositions();

    public static SoftArmorBalanceSettings Default { get; } = new();

    private static Dictionary<string, SoftArmorClassBalance> CreateDefaultClasses()
    {
        return new Dictionary<string, SoftArmorClassBalance>(StringComparer.OrdinalIgnoreCase)
        {
            ["3"] = new()
            {
                BaseDurability = 85,
                BluntThroughput = 0.36,
                FullSetWeightKg = 1.2,
                FullSetSpeedPenaltyPercent = 0,
                FullSetMousePenalty = 0,
                FullSetErgonomicPenalty = 0,
                RepairCost = 85,
                FrontBackFleaPrice = 25_000,
                FrontBackHandbookPrice = 20_000,
                StaticLootWeight = 300,
                WaylandStock = 8
            },
            ["4"] = new()
            {
                BaseDurability = 75,
                BluntThroughput = 0.34,
                FullSetWeightKg = 2.0,
                FullSetSpeedPenaltyPercent = -1,
                FullSetMousePenalty = -0.5,
                FullSetErgonomicPenalty = -2,
                RepairCost = 150,
                FrontBackFleaPrice = 50_000,
                FrontBackHandbookPrice = 40_000,
                StaticLootWeight = 140,
                WaylandStock = 5
            },
            ["5"] = new()
            {
                BaseDurability = 65,
                BluntThroughput = 0.31,
                FullSetWeightKg = 3.2,
                FullSetSpeedPenaltyPercent = -2.5,
                FullSetMousePenalty = -1.5,
                FullSetErgonomicPenalty = -5,
                RepairCost = 250,
                FrontBackFleaPrice = 110_000,
                FrontBackHandbookPrice = 85_000,
                StaticLootWeight = 45,
                WaylandStock = 2
            },
            ["6"] = new()
            {
                BaseDurability = 50,
                BluntThroughput = 0.28,
                FullSetWeightKg = 4.8,
                FullSetSpeedPenaltyPercent = -5,
                FullSetMousePenalty = -3,
                FullSetErgonomicPenalty = -9,
                RepairCost = 450,
                FrontBackFleaPrice = 260_000,
                FrontBackHandbookPrice = 200_000,
                StaticLootWeight = 8,
                WaylandStock = 1
            }
        };
    }

    private static Dictionary<string, SoftArmorPositionBalance> CreateDefaultPositions()
    {
        return new Dictionary<string, SoftArmorPositionBalance>(StringComparer.OrdinalIgnoreCase)
        {
            ["front"] = new() { DurabilityMultiplier = 1.0, SetShare = 0.22, PriceMultiplier = 1.0 },
            ["back"] = new() { DurabilityMultiplier = 1.0, SetShare = 0.22, PriceMultiplier = 1.0 },
            ["side"] = new() { DurabilityMultiplier = 0.40, SetShare = 0.09, PriceMultiplier = 0.50 },
            ["groin"] = new() { DurabilityMultiplier = 0.50, SetShare = 0.12, PriceMultiplier = 0.60 },
            ["shoulder"] = new() { DurabilityMultiplier = 0.30, SetShare = 0.08, PriceMultiplier = 0.35 },
            ["collar"] = new() { DurabilityMultiplier = 0.25, SetShare = 0.10, PriceMultiplier = 0.30 }
        };
    }
}

public sealed class SoftArmorClassBalance
{
    public int BaseDurability { get; init; } = 70;
    public double BluntThroughput { get; init; } = 0.32;
    public double FullSetWeightKg { get; init; }
    public double FullSetSpeedPenaltyPercent { get; init; }
    public double FullSetMousePenalty { get; init; }
    public double FullSetErgonomicPenalty { get; init; }
    public int RepairCost { get; init; } = 85;
    public int FrontBackFleaPrice { get; init; } = 30_000;
    public int FrontBackHandbookPrice { get; init; } = 25_000;
    public int StaticLootWeight { get; init; } = 100;
    public int WaylandStock { get; init; } = 2;
}

public sealed class SoftArmorPositionBalance
{
    public double DurabilityMultiplier { get; init; } = 1.0;
    public double SetShare { get; init; } = 1.0;
    public double PriceMultiplier { get; init; } = 1.0;
}
