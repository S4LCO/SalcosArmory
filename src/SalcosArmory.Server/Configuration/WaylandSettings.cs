namespace SalcosArmory.Config;

public sealed class WaylandSettings
{
    public bool ShowOffersOnFlea { get; init; } = true;
    public string PriceSource { get; init; } = "flea";
    public double PriceMultiplier { get; init; } = 1.0;
    public int MinimumPrice { get; init; } = 1;
    public int RefreshTimeMinMinutes { get; init; } = 60;
    public int RefreshTimeMaxMinutes { get; init; } = 120;
    public Dictionary<string, WaylandCategorySettings> Categories { get; init; } = new();
    public Dictionary<string, WaylandItemOverride> ItemOverrides { get; init; } = new();

    public static WaylandSettings Default { get; } = new();
}

public sealed class WaylandCategorySettings
{
    public int LoyaltyLevel { get; init; } = 1;
    public int Stock { get; init; } = 3;
}

public sealed class WaylandItemOverride
{
    public bool Enabled { get; init; } = true;
    public int? LoyaltyLevel { get; init; }
    public int? Stock { get; init; }
    public int? Price { get; init; }
}
