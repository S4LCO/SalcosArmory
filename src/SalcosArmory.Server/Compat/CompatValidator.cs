using System.Text;
using SPTarkov.DI.Annotations;

namespace SalcosArmory.Compat;

[Injectable(InjectionType.Singleton)]
public sealed class CompatValidator
{
    public bool TryCleanFile(
        string fileName,
        CompatRuleSet set,
        out List<CompatRule> rules,
        out string error)
    {
        rules = [];
        error = string.Empty;

        var sourceRules = set.Rules ?? [];
        if (sourceRules.Count == 0)
        {
            return true;
        }

        var errors = new StringBuilder();

        for (var i = 0; i < sourceRules.Count; i++)
        {
            if (TryCleanRule(fileName, i + 1, sourceRules[i], out var rule, out var ruleError))
            {
                rules.Add(rule);
                continue;
            }

            AppendError(errors, ruleError);
        }

        if (errors.Length == 0)
        {
            return true;
        }

        error = errors.ToString();
        rules.Clear();
        return false;
    }

    private static bool TryCleanRule(
        string fileName,
        int ruleNumber,
        CompatRule input,
        out CompatRule rule,
        out string error)
    {
        rule = new CompatRule();
        error = string.Empty;

        var target = input.TargetTpl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(target))
        {
            error = $"Rule #{ruleNumber} in '{fileName}' has an empty TARGET-TPL.";
            return false;
        }

        var allowed = (input.AllowedTpls ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allowed.Count == 0)
        {
            error = $"Rule #{ruleNumber} in '{fileName}' has no valid ALLOWED_TPLS.";
            return false;
        }

        var indexes = input.GridIndexes?
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (indexes is not null && indexes.Any(x => x < 0))
        {
            error = $"Rule #{ruleNumber} in '{fileName}' contains negative GRID_INDEXES.";
            return false;
        }

        var slotNames = input.SlotNames?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (input.SlotNames is not null && slotNames is { Count: 0 })
        {
            error = $"Rule #{ruleNumber} in '{fileName}' contains no valid SLOT_NAMES.";
            return false;
        }

        rule = new CompatRule
        {
            TargetTpl = target,
            AllowedTpls = allowed,
            GridIndexes = indexes,
            SlotNames = slotNames is { Count: > 0 } ? slotNames : null,
            Replace = input.Replace
        };

        return true;
    }

    private static void AppendError(StringBuilder builder, string message)
    {
        if (builder.Length > 0)
        {
            builder.Append(" | ");
        }

        builder.Append(message);
    }
}