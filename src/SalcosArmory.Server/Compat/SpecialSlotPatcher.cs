using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SalcosArmory.Compat;

[Injectable(InjectionType.Singleton)]
public sealed class SpecialSlotPatcher
{
    private const string PmcSpecialSlotsToken = "@PMC_SPECIALSLOTS";
    private const string AllSpecialSlotsToken = "@GLOBAL_SPECIALSLOTS";

    public static bool IsSpecialSlotTarget(string target)
    {
        return string.Equals(target, PmcSpecialSlotsToken, StringComparison.OrdinalIgnoreCase)
            || string.Equals(target, AllSpecialSlotsToken, StringComparison.OrdinalIgnoreCase);
    }

    public bool Apply(
        Dictionary<MongoId, TemplateItem> items,
        CompatRule rule,
        CompatReport report,
        out string reason)
    {
        reason = string.Empty;

        var matches = new List<(Slot Slot, int Number)>();
        foreach (var item in items.Values)
        {
            var slots = item.Properties?.Slots;
            if (slots is null)
            {
                continue;
            }

            foreach (var slot in slots)
            {
                if (slot?.Name is null || !slot.Name.Contains("SpecialSlot", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matches.Add((slot, ReadTrailingNumber(slot.Name)));
            }
        }

        report.SpecialSlotsMatched += matches.Count;
        if (matches.Count == 0)
        {
            reason = "No special slot candidates were found.";
            return false;
        }

        var wanted = rule.GridIndexes is { Count: > 0 }
            ? rule.GridIndexes.Where(x => x is >= 1 and <= 3).ToHashSet()
            : new HashSet<int> { 1, 2, 3 };

        if (wanted.Count == 0)
        {
            reason = "Special slot rule has no valid indexes. Use 1, 2 or 3.";
            return false;
        }

        report.TargetsFound++;

        foreach (var (slot, number) in matches)
        {
            if (!wanted.Contains(number))
            {
                continue;
            }

            var filters = slot.Properties?.Filters;
            if (filters is null)
            {
                continue;
            }

            foreach (var filter in filters)
            {
                if (filter?.Filter is not null)
                {
                    ItemSlotPatcher.PatchFilter(filter.Filter, rule, report);
                }
            }

            report.SpecialSlotsPatched++;
        }

        return true;
    }

    private static int ReadTrailingNumber(string value)
    {
        var digits = new string(value.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var number) ? number : 0;
    }
}
