using System.Text.Json;
using System.Text.Json.Nodes;
using SalcosArmory.Config;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Content;

[Injectable(InjectionType.Singleton)]
public sealed class SoftArmorBalanceService(ISptLogger<SoftArmorBalanceService> logger)
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions OutputOptions = new()
    {
        WriteIndented = true
    };

    private SoftArmorBalanceSettings _settings = SoftArmorBalanceSettings.Default;
    private readonly Dictionary<string, int> _maxDurabilityByTemplateId =
        new(StringComparer.OrdinalIgnoreCase);

    public bool Enabled => _settings.Enabled;

    public bool HasRegisteredTemplates => _maxDurabilityByTemplateId.Count > 0;

    public void Configure(SoftArmorBalanceSettings settings)
    {
        _settings = settings;

        if (!_settings.Enabled)
        {
            logger.Info(Log.Line("Soft armor insert rebalance is disabled."));
            return;
        }

        logger.Info(Log.Line(
            $"Soft armor insert rebalance configured for {_settings.Classes.Count} class tier(s)."));
    }

    public bool TryTransform(string filePath, string relativePath, out string transformedJson)
    {
        transformedJson = string.Empty;

        if (!TryResolve(relativePath, out var plan))
        {
            return false;
        }

        var root = JsonNode.Parse(
            File.ReadAllText(filePath),
            documentOptions: DocumentOptions) as JsonObject;

        if (root is null)
        {
            return false;
        }

        var changed = false;
        foreach (var property in root.ToArray())
        {
            if (property.Value is not JsonObject item)
            {
                continue;
            }

            var overrides = item["overrideProperties"] as JsonObject;
            if (overrides is null)
            {
                continue;
            }

            overrides["Durability"] = plan.Durability;
            overrides["MaxDurability"] = plan.Durability;
            overrides["BluntThroughput"] = plan.BluntThroughput;
            overrides["Weight"] = plan.WeightKg;
            overrides["speedPenaltyPercent"] = plan.SpeedPenaltyPercent;
            overrides["mousePenalty"] = plan.MousePenalty;
            overrides["weaponErgonomicPenalty"] = plan.ErgonomicPenalty;
            overrides["RepairCost"] = plan.RepairCost;

            _maxDurabilityByTemplateId[property.Key] = plan.Durability;

            item["fleaPriceRoubles"] = plan.FleaPrice;
            item["handbookPriceRoubles"] = plan.HandbookPrice;

            if (item["staticLootContainers"] is JsonArray containers)
            {
                foreach (var container in containers.OfType<JsonObject>())
                {
                    container["probability"] = plan.StaticLootWeight;
                }
            }

            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        transformedJson = root.ToJsonString(OutputOptions);
        return true;
    }

    public bool TryResolveOffer(
        string relativePath,
        out int handbookPrice,
        out int fleaPrice,
        out int waylandStock)
    {
        if (TryResolve(relativePath, out var plan))
        {
            handbookPrice = plan.HandbookPrice;
            fleaPrice = plan.FleaPrice;
            waylandStock = plan.WaylandStock;
            return true;
        }

        handbookPrice = 0;
        fleaPrice = 0;
        waylandStock = 0;
        return false;
    }

    public bool TryGetMaxDurability(string templateId, out int maxDurability)
    {
        return _maxDurabilityByTemplateId.TryGetValue(templateId, out maxDurability);
    }

    private bool TryResolve(string relativePath, out SoftArmorBalancePlan plan)
    {
        plan = default;

        if (!_settings.Enabled
            || !relativePath.Contains("soft_armor_inserts_body", StringComparison.OrdinalIgnoreCase)
            || !TryReadArmorClass(relativePath, out var armorClass)
            || !TryReadPosition(relativePath, out var position)
            || !TryGetClassBalance(armorClass, out var classBalance)
            || !TryGetPositionBalance(position, out var positionBalance))
        {
            return false;
        }

        plan = new SoftArmorBalancePlan(
            Durability: Math.Max(1, (int)Math.Round(
                classBalance.BaseDurability * Math.Max(0.01, positionBalance.DurabilityMultiplier),
                MidpointRounding.AwayFromZero)),
            BluntThroughput: Math.Round(Math.Clamp(classBalance.BluntThroughput, 0, 1), 3),
            WeightKg: Scale(classBalance.FullSetWeightKg, positionBalance.SetShare, 3),
            SpeedPenaltyPercent: Scale(classBalance.FullSetSpeedPenaltyPercent, positionBalance.SetShare, 3),
            MousePenalty: Scale(classBalance.FullSetMousePenalty, positionBalance.SetShare, 3),
            ErgonomicPenalty: Scale(classBalance.FullSetErgonomicPenalty, positionBalance.SetShare, 3),
            RepairCost: Math.Max(0, classBalance.RepairCost),
            FleaPrice: ScalePrice(classBalance.FrontBackFleaPrice, positionBalance.PriceMultiplier),
            HandbookPrice: ScalePrice(classBalance.FrontBackHandbookPrice, positionBalance.PriceMultiplier),
            StaticLootWeight: Math.Max(0, classBalance.StaticLootWeight),
            WaylandStock: Math.Max(1, classBalance.WaylandStock));

        return true;
    }

    private bool TryGetClassBalance(string armorClass, out SoftArmorClassBalance balance)
    {
        var match = _settings.Classes.FirstOrDefault(pair =>
            pair.Key.Equals(armorClass, StringComparison.OrdinalIgnoreCase));
        balance = match.Value!;
        return balance is not null;
    }

    private bool TryGetPositionBalance(string position, out SoftArmorPositionBalance balance)
    {
        var match = _settings.Positions.FirstOrDefault(pair =>
            pair.Key.Equals(position, StringComparison.OrdinalIgnoreCase));
        balance = match.Value!;
        return balance is not null;
    }

    private static bool TryReadArmorClass(string relativePath, out string armorClass)
    {
        for (var level = 3; level <= 6; level++)
        {
            if (relativePath.Contains($"/Level_{level}/", StringComparison.OrdinalIgnoreCase))
            {
                armorClass = level.ToString();
                return true;
            }
        }

        armorClass = string.Empty;
        return false;
    }

    private static bool TryReadPosition(string relativePath, out string position)
    {
        var name = Path.GetFileNameWithoutExtension(relativePath).Trim().ToLowerInvariant();

        position = name switch
        {
            _ when name.StartsWith("soft_armor_front") => "front",
            _ when name.StartsWith("soft_armor_back") => "back",
            _ when name.StartsWith("soft_armor_left") => "side",
            _ when name.StartsWith("soft_armor_right") => "side",
            _ when name.StartsWith("armor_groin") => "groin",
            _ when name.StartsWith("shoulder_left") => "shoulder",
            _ when name.StartsWith("shoulder_right") => "shoulder",
            _ when name.StartsWith("collar") => "collar",
            _ => string.Empty
        };

        return position.Length > 0;
    }

    private static double Scale(double value, double share, int digits)
    {
        return Math.Round(
            value * Math.Max(0, share),
            digits,
            MidpointRounding.AwayFromZero);
    }

    private static int ScalePrice(int value, double multiplier)
    {
        return Math.Max(1, (int)Math.Round(
            value * Math.Max(0.01, multiplier),
            MidpointRounding.AwayFromZero));
    }

    private readonly record struct SoftArmorBalancePlan(
        int Durability,
        double BluntThroughput,
        double WeightKg,
        double SpeedPenaltyPercent,
        double MousePenalty,
        double ErgonomicPenalty,
        int RepairCost,
        int FleaPrice,
        int HandbookPrice,
        int StaticLootWeight,
        int WaylandStock);
}
