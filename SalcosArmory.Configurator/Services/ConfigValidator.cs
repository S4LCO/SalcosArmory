using SalcosArmory.Configurator.Models;

namespace SalcosArmory.Configurator.Services;

internal static class ConfigValidator
{
    public static ValidationResult Validate(ConfigWorkspace workspace)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        ValidateCountermeasures(workspace.Countermeasures, errors);
        ValidateWayland(workspace.Wayland, errors);
        ValidateSoftArmor(workspace.SoftArmor, errors, warnings);
        ValidateRuntimeInjection(workspace.RuntimeInjection, errors);

        return new ValidationResult(errors, warnings);
    }

    private static void ValidateCountermeasures(CountermeasureConfig config, List<string> errors)
    {
        InRange(config.HistorySize, 3, 20, "Countermeasures: History size", errors);
        InRange(config.MinimumRaids, 1, config.HistorySize, "Countermeasures: Minimum raids", errors);
        InRange(config.HistoryDecay, 0.1, 1, "Countermeasures: History decay", errors);
        InRange(config.MinimumAffectedPercent, 0, 100, "Countermeasures: Minimum affected percent", errors);
        InRange(config.MaximumAffectedPercent, 0, 100, "Countermeasures: Maximum affected percent", errors);

        if (config.MaximumAffectedPercent < config.MinimumAffectedPercent)
        {
            errors.Add("Countermeasures: Maximum affected percent cannot be lower than the minimum.");
        }

        InRange(config.MaximumCountermeasuresPerBot, 1, 5, "Countermeasures: Maximum measures per bot", errors);
        InRange(config.HeavyArmorClassThreshold, 1, 6, "Countermeasures: Heavy armor class", errors);
        InRange(config.NightRaidThreshold, 0, 1, "Countermeasures: Night raid threshold", errors);
        InRange(config.HeadshotRatioThreshold, 0, 1, "Countermeasures: Headshot threshold", errors);
        InRange(config.LongRangeDistanceThreshold, 1, 500, "Countermeasures: Long-range distance", errors);
        InRange(config.SuppressorUsageThreshold, 0, 1, "Countermeasures: Suppressor threshold", errors);
        InRange(config.HeavyArmorUsageThreshold, 0, 1, "Countermeasures: Heavy-armor usage", errors);
        InRange(config.SurvivalRateThreshold, 0, 1, "Countermeasures: Survival threshold", errors);
        InRange(config.MaximumAttachmentDepth, 1, 4, "Countermeasures: Attachment depth", errors);
        InRange(config.AmmoPenetrationIncrease, 0, 30, "Countermeasures: Penetration increase", errors);
        InRange(config.AmmoPenetrationCap, 0, 100, "Countermeasures: Penetration cap", errors);
    }

    private static void ValidateWayland(WaylandConfig config, List<string> errors)
    {
        if (!config.PriceSource.Equals("flea", StringComparison.OrdinalIgnoreCase)
            && !config.PriceSource.Equals("handbook", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Wayland: Price source must be either 'flea' or 'handbook'.");
        }

        if (config.PriceMultiplier <= 0)
        {
            errors.Add("Wayland: Price multiplier must be greater than zero.");
        }

        if (config.MinimumPrice < 1)
        {
            errors.Add("Wayland: Minimum price must be at least 1 rouble.");
        }

        if (config.RefreshTimeMinMinutes < 1
            || config.RefreshTimeMaxMinutes < config.RefreshTimeMinMinutes)
        {
            errors.Add("Wayland: Refresh maximum must be equal to or greater than a positive minimum.");
        }

        ValidateUniqueNames(config.Categories.Select(x => x.Name), "Wayland category", errors);
        foreach (var category in config.Categories)
        {
            InRange(category.LoyaltyLevel, 1, 4, $"Wayland category '{category.Name}': Loyalty level", errors);
            if (category.Stock < 0)
            {
                errors.Add($"Wayland category '{category.Name}': Stock cannot be negative.");
            }
        }

        ValidateUniqueNames(config.ItemOverrides.Select(x => x.TemplateId), "Wayland item template", errors);
        foreach (var item in config.ItemOverrides)
        {
            ValidateTemplateId(item.TemplateId, "Wayland item template", errors);
            if (item.LoyaltyLevel is not null)
            {
                InRange(item.LoyaltyLevel.Value, 1, 4, $"Wayland item '{item.TemplateId}': Loyalty level", errors);
            }

            if (item.Stock < 0)
            {
                errors.Add($"Wayland item '{item.TemplateId}': Stock cannot be negative.");
            }

            if (item.Price is <= 0)
            {
                errors.Add($"Wayland item '{item.TemplateId}': A fixed price must be greater than zero.");
            }
        }
    }

    private static void ValidateSoftArmor(
        SoftArmorConfig config,
        List<string> errors,
        List<string> warnings)
    {
        ValidateUniqueNames(config.Classes.Select(x => x.ArmorClass), "Soft armor class", errors);
        foreach (var armorClass in config.Classes)
        {
            if (armorClass.BaseDurability < 1)
            {
                errors.Add($"Soft armor class '{armorClass.ArmorClass}': Durability must be at least 1.");
            }

            InRange(armorClass.BluntThroughput, 0, 1, $"Soft armor class '{armorClass.ArmorClass}': Blunt throughput", errors);
            NonNegative(armorClass.FullSetWeightKg, $"Soft armor class '{armorClass.ArmorClass}': Weight", errors);
            NonNegative(armorClass.RepairCost, $"Soft armor class '{armorClass.ArmorClass}': Repair cost", errors);
            NonNegative(armorClass.FrontBackFleaPrice, $"Soft armor class '{armorClass.ArmorClass}': Flea price", errors);
            NonNegative(armorClass.FrontBackHandbookPrice, $"Soft armor class '{armorClass.ArmorClass}': Handbook price", errors);
            NonNegative(armorClass.StaticLootWeight, $"Soft armor class '{armorClass.ArmorClass}': Loot weight", errors);
            if (armorClass.WaylandStock < 1)
            {
                errors.Add($"Soft armor class '{armorClass.ArmorClass}': Wayland stock must be at least 1.");
            }
        }

        ValidateUniqueNames(config.Positions.Select(x => x.Position), "Soft armor position", errors);
        foreach (var position in config.Positions)
        {
            if (position.DurabilityMultiplier <= 0 || position.PriceMultiplier <= 0)
            {
                errors.Add($"Soft armor position '{position.Position}': Durability and price multipliers must be greater than zero.");
            }

            NonNegative(position.SetShare, $"Soft armor position '{position.Position}': Set share", errors);
        }

        var fullSetShare = config.Positions.Sum(position => position.Position.Equals("side", StringComparison.OrdinalIgnoreCase)
            || position.Position.Equals("shoulder", StringComparison.OrdinalIgnoreCase)
                ? position.SetShare * 2
                : position.SetShare);
        if (Math.Abs(fullSetShare - 1) > 0.01)
        {
            warnings.Add($"Soft armor: The calculated full-set shares total {fullSetShare:0.###} instead of 1.0.");
        }
    }

    private static void ValidateRuntimeInjection(RuntimeInjectionConfig config, List<string> errors)
    {
        ValidateUniqueNames(config.Targets.Select(x => x.ItemTpl), "Runtime injection target", errors);
        foreach (var target in config.Targets)
        {
            ValidateTemplateId(target.ItemTpl, "Runtime injection target", errors);
            ValidateUniqueNames(target.Slots.Select(x => x.SlotName), $"Runtime target '{target.ItemTpl}' slot", errors);

            foreach (var slot in target.Slots)
            {
                if (string.IsNullOrWhiteSpace(slot.SlotName))
                {
                    errors.Add($"Runtime target '{target.ItemTpl}': Slot names cannot be empty.");
                }

                ValidateTemplateId(slot.ItemTpl, $"Runtime slot '{slot.SlotName}' item", errors);
            }
        }
    }

    private static void ValidateUniqueNames(IEnumerable<string> names, string label, List<string> errors)
    {
        var values = names.ToArray();
        if (values.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add($"{label}: Names cannot be empty.");
        }

        var duplicate = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            errors.Add($"{label}: '{duplicate}' is listed more than once.");
        }
    }

    private static void ValidateTemplateId(string value, string label, List<string> errors)
    {
        if (value.Length != 24 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            errors.Add($"{label}: '{value}' is not a valid 24-character template ID.");
        }
    }

    private static void InRange(double value, double minimum, double maximum, string label, List<string> errors)
    {
        if (value < minimum || value > maximum)
        {
            errors.Add($"{label} must be between {minimum:0.###} and {maximum:0.###}.");
        }
    }

    private static void NonNegative(double value, string label, List<string> errors)
    {
        if (value < 0)
        {
            errors.Add($"{label} cannot be negative.");
        }
    }
}

internal sealed record ValidationResult(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool IsValid => Errors.Count == 0;
}
