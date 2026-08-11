using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SalcosArmory.Compat;

[Injectable(InjectionType.Singleton)]
public sealed class ItemSlotPatcher
{
    public bool Apply(
        Dictionary<MongoId, TemplateItem> items,
        CompatRule rule,
        CompatReport report,
        out string reason)
    {
        reason = string.Empty;

        if (!items.TryGetValue(new MongoId(rule.TargetTpl), out var item) || item.Properties is null)
        {
            reason = $"Target '{rule.TargetTpl}' was not found.";
            return false;
        }

        report.TargetsFound++;
        PatchGrids(item.Properties, rule, report);
        PatchSlots(item.Properties, rule, report);
        return true;
    }

    private static void PatchGrids(TemplateItemProperties props, CompatRule rule, CompatReport report)
    {
        var grids = props.Grids?.ToList();
        if (grids is null || grids.Count == 0)
        {
            return;
        }

        if (rule.SlotNames is { Count: > 0 } && rule.GridIndexes is not { Count: > 0 })
        {
            return;
        }

        var indexes = rule.GridIndexes is { Count: > 0 }
            ? rule.GridIndexes.Where(i => i >= 0 && i < grids.Count)
            : Enumerable.Range(0, grids.Count);

        foreach (var index in indexes)
        {
            var filters = grids[index].Properties?.Filters;
            if (filters is null)
            {
                continue;
            }

            foreach (var filter in filters)
            {
                if (filter?.Filter is not null)
                {
                    PatchFilter(filter.Filter, rule, report);
                }
            }
        }
    }

    private static void PatchSlots(TemplateItemProperties props, CompatRule rule, CompatReport report)
    {
        var slots = props.Slots?.ToList();
        if (slots is null || slots.Count == 0)
        {
            return;
        }

        var selected = SelectSlots(slots, rule);
        foreach (var slot in selected)
        {
            var filters = slot?.Properties?.Filters;
            if (filters is null)
            {
                continue;
            }

            foreach (var filter in filters)
            {
                if (filter?.Filter is not null)
                {
                    PatchFilter(filter.Filter, rule, report);
                }
            }
        }
    }

    private static IEnumerable<Slot?> SelectSlots(IReadOnlyList<Slot?> slots, CompatRule rule)
    {
        if (rule.SlotNames is { Count: > 0 })
        {
            var names = new HashSet<string>(rule.SlotNames, StringComparer.OrdinalIgnoreCase);
            return slots.Where(slot => slot?.Name is not null && names.Contains(slot.Name));
        }

        var indexes = rule.GridIndexes is { Count: > 0 }
            ? rule.GridIndexes.Where(i => i >= 0 && i < slots.Count)
            : Enumerable.Range(0, slots.Count);

        return indexes.Select(i => slots[i]);
    }

    internal static void PatchFilter(HashSet<MongoId> filter, CompatRule rule, CompatReport report)
    {
        report.FiltersTouched++;

        if (rule.Replace)
        {
            filter.Clear();
            foreach (var tpl in rule.AllowedTpls)
            {
                filter.Add(new MongoId(tpl));
            }

            report.Replaced += rule.AllowedTpls.Count;
            return;
        }

        foreach (var tpl in rule.AllowedTpls)
        {
            if (filter.Add(new MongoId(tpl)))
            {
                report.Added++;
            }
        }
    }
}
