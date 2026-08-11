using SalcosArmory.Config;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SalcosArmory.Runtime;

[Injectable(InjectionType.Singleton)]
public sealed class RuntimeInjectionValidator(TemplateTable templateTable)
{
    public RuntimeInjectionValidation Validate(RuntimeInjectionSettings settings)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        var resolvedTargets = new List<ResolvedRuntimeInjectionTarget>();
        var seenHosts = new HashSet<MongoId>();
        var items = templateTable.Items;
        var configuredTargets = settings.Targets ?? [];

        for (var index = 0; index < configuredTargets.Count; index++)
        {
            var target = configuredTargets[index];
            var label = $"Runtime target #{index + 1}";

            if (target is null || !TryReadMongoId(target.ItemTpl, out var hostTpl))
            {
                errors.Add($"{label} has an empty or invalid itemTpl.");
                continue;
            }

            if (!seenHosts.Add(hostTpl))
            {
                errors.Add($"{label} repeats host tpl '{hostTpl}'. Put all slot mappings for a host in one target.");
                continue;
            }

            var mappings = ReadMappings(label, target.Slots, errors);
            if (mappings is null)
            {
                continue;
            }

            if (!items.TryGetValue(hostTpl, out var hostTemplate))
            {
                warnings.Add($"{label}: host tpl '{hostTpl}' is not present in the item database.");
                continue;
            }

            var hostSlots = hostTemplate.Properties?.Slots?.Where(slot => slot is not null).ToList();
            if (hostSlots is null || hostSlots.Count == 0)
            {
                warnings.Add($"{label}: host tpl '{hostTpl}' has no slots.");
                continue;
            }

            var resolvedSlots = ResolveSlots(label, hostTpl, mappings, hostSlots, items, warnings);
            if (resolvedSlots.Count > 0)
            {
                resolvedTargets.Add(new ResolvedRuntimeInjectionTarget(hostTpl, resolvedSlots));
            }
        }

        return new RuntimeInjectionValidation(configuredTargets.Count, resolvedTargets, warnings, errors);
    }

    private static Dictionary<string, MongoId>? ReadMappings(
        string label,
        Dictionary<string, string>? configuredSlots,
        ICollection<string> errors)
    {
        if (configuredSlots is null || configuredSlots.Count == 0)
        {
            errors.Add($"{label} has no slot mappings.");
            return null;
        }

        var mappings = new Dictionary<string, MongoId>(StringComparer.OrdinalIgnoreCase);
        var valid = true;

        foreach (var (slotName, configuredTpl) in configuredSlots)
        {
            if (string.IsNullOrWhiteSpace(slotName) || !TryReadMongoId(configuredTpl, out var itemTpl))
            {
                errors.Add($"{label} contains an empty slot name or invalid insert tpl.");
                valid = false;
                continue;
            }

            mappings[slotName.Trim()] = itemTpl;
        }

        return valid ? mappings : null;
    }

    private static Dictionary<string, MongoId> ResolveSlots(
        string label,
        MongoId hostTpl,
        IReadOnlyDictionary<string, MongoId> mappings,
        IReadOnlyCollection<Slot?> hostSlots,
        IReadOnlyDictionary<MongoId, TemplateItem> items,
        ICollection<string> warnings)
    {
        var resolved = new Dictionary<string, MongoId>(StringComparer.OrdinalIgnoreCase);

        foreach (var (configuredSlotName, insertTpl) in mappings)
        {
            var slot = hostSlots.FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate?.Name)
                && string.Equals(candidate.Name, configuredSlotName, StringComparison.OrdinalIgnoreCase));

            if (slot is null)
            {
                warnings.Add($"{label}: slot '{configuredSlotName}' does not exist on host tpl '{hostTpl}'.");
                continue;
            }

            if (!items.ContainsKey(insertTpl))
            {
                warnings.Add($"{label}: insert tpl '{insertTpl}' is not present in the item database.");
                continue;
            }

            if (!ItemSlotRules.Allows(slot, insertTpl))
            {
                warnings.Add($"{label}: insert tpl '{insertTpl}' is not allowed in slot '{slot.Name}'.");
                continue;
            }

            resolved[slot.Name!] = insertTpl;
        }

        if (resolved.Count == 0)
        {
            warnings.Add($"{label}: no usable slot mappings remain after validation.");
        }

        return resolved;
    }

    private static bool TryReadMongoId(string? value, out MongoId mongoId)
    {
        mongoId = default;

        if (string.IsNullOrWhiteSpace(value) || !MongoId.IsValidMongoId(value))
        {
            return false;
        }

        mongoId = new MongoId(value.Trim());
        return true;
    }
}

public sealed record RuntimeInjectionValidation(
    int ConfiguredTargetCount,
    IReadOnlyList<ResolvedRuntimeInjectionTarget> Targets,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors
);

public sealed record ResolvedRuntimeInjectionTarget(
    MongoId HostTpl,
    IReadOnlyDictionary<string, MongoId> Slots
);
