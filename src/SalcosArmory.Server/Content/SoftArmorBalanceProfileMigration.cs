using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Migration;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Content;

/// <summary>
/// Normalizes already-owned insert instances once when a profile first loads on 0.5.1.
/// The item's remaining durability percentage is preserved; no item is deleted or moved.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class SoftArmorBalanceProfileMigration(
    SoftArmorBalanceService balanceService,
    ISptLogger<SoftArmorBalanceProfileMigration> logger) : AbstractProfileMigration
{
    public override string MigrationName => "SalcosArmorySoftArmor051";

    public override bool CanMigrate(
        JsonObject profile,
        IEnumerable<IProfileMigration> previouslyRanMigrations)
    {
        return balanceService.Enabled
            && balanceService.HasRegisteredTemplates
            && NeedsNormalization(profile);
    }

    public override JsonObject Migrate(JsonObject profile)
    {
        var normalized = NormalizeNode(profile);
        logger.Info(Log.Line(
            $"Soft armor profile migration normalized {normalized} existing insert instance(s)."));
        return base.Migrate(profile) ?? profile;
    }

    private bool NeedsNormalization(JsonNode? node)
    {
        if (node is JsonObject item)
        {
            if (TryGetRepairable(item, out _, out var targetMax, out _, out var storedMax)
                && Math.Abs(storedMax - targetMax) > 0.001)
            {
                return true;
            }

            return item.Any(property => NeedsNormalization(property.Value));
        }

        return node is JsonArray array && array.Any(NeedsNormalization);
    }

    private int NormalizeNode(JsonNode? node)
    {
        if (node is JsonObject item)
        {
            var normalized = 0;
            if (TryGetRepairable(
                    item,
                    out var repairable,
                    out var targetMax,
                    out var currentDurability,
                    out var storedMax)
                && Math.Abs(storedMax - targetMax) > 0.001)
            {
                var remainingShare = storedMax > 0
                    ? Math.Clamp(currentDurability / storedMax, 0, 1)
                    : 1;
                var normalizedCurrent = Math.Round(
                    targetMax * remainingShare,
                    2,
                    MidpointRounding.AwayFromZero);

                repairable["MaxDurability"] = targetMax;
                repairable["Durability"] = Math.Min(targetMax, normalizedCurrent);
                normalized++;
            }

            foreach (var property in item.ToArray())
            {
                normalized += NormalizeNode(property.Value);
            }

            return normalized;
        }

        if (node is JsonArray array)
        {
            return array.Sum(NormalizeNode);
        }

        return 0;
    }

    private bool TryGetRepairable(
        JsonObject item,
        out JsonObject repairable,
        out int targetMax,
        out double currentDurability,
        out double storedMax)
    {
        repairable = null!;
        targetMax = 0;
        currentDurability = 0;
        storedMax = 0;

        if (!TryReadString(item["_tpl"], out var templateId)
            || !balanceService.TryGetMaxDurability(templateId, out targetMax)
            || item["upd"] is not JsonObject upd
            || upd["Repairable"] is not JsonObject repairableObject
            || !TryReadNumber(repairableObject["MaxDurability"], out storedMax)
            || !TryReadNumber(repairableObject["Durability"], out currentDurability))
        {
            return false;
        }

        repairable = repairableObject;
        return true;
    }

    private static bool TryReadString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue
            || !jsonValue.TryGetValue(out string? parsedValue)
            || string.IsNullOrWhiteSpace(parsedValue))
        {
            return false;
        }

        value = parsedValue;
        return true;
    }

    private static bool TryReadNumber(JsonNode? node, out double value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
        {
            return false;
        }

        if (jsonValue.TryGetValue(out double doubleValue))
        {
            value = doubleValue;
            return true;
        }

        if (jsonValue.TryGetValue(out int intValue))
        {
            value = intValue;
            return true;
        }

        return false;
    }
}
