using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Compat;

[Injectable(InjectionType.Singleton)]
public sealed class CompatService(
    CompatFileReader reader,
    CompatValidator validator,
    ItemSlotPatcher itemSlotPatcher,
    SpecialSlotPatcher specialSlotPatcher,
    TemplateTable templateTable,
    ISptLogger<CompatService> logger
)
{
    public async Task<ModuleResult> LoadAsync(ArmoryPaths paths)
    {
        var files = Files.EnumerateJson(paths.CompatItems)
            .Concat(Files.EnumerateJson(paths.CompatWeapons))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            return ModuleResult.Skipped("Compat", "No compat files found.");
        }

        var configs = new List<(string FilePath, List<CompatRule> Rules)>();
        var skippedFiles = 0;

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            var set = await reader.ReadAsync(file);

            if (set.Rules.Count == 0)
            {
                skippedFiles++;
                logger.Info(Log.Line($"Compat: {fileName} has no rules."));
                continue;
            }

            if (!validator.TryCleanFile(fileName, set, out var rules, out var error))
            {
                logger.Error(Log.Line($"Compat: {error}"));
                return ModuleResult.Failed("Compat", $"Validation failed in '{fileName}'.");
            }

            if (rules.Count == 0)
            {
                skippedFiles++;
                logger.Info(Log.Line($"Compat: {fileName} has no usable rules."));
                continue;
            }

            configs.Add((file, rules));
        }

        if (configs.Count == 0)
        {
            return ModuleResult.Skipped("Compat", $"No applicable compat rules found. SkippedFiles={skippedFiles}.");
        }

        var items = templateTable.Items;
        var report = new CompatReport
        {
            Files = configs.Count,
            Rules = configs.Sum(x => x.Rules.Count)
        };

        var warnings = 0;

        foreach (var (filePath, rules) in configs)
        {
            var fileName = Path.GetFileName(filePath);

            foreach (var rule in rules)
            {
                CountMissingAllowedTemplates(items, rule, report);

                var applied = SpecialSlotPatcher.IsSpecialSlotTarget(rule.TargetTpl)
                    ? specialSlotPatcher.Apply(items, rule, report, out var reason)
                    : itemSlotPatcher.Apply(items, rule, report, out reason);

                if (applied)
                {
                    continue;
                }

                warnings++;
                report.TargetsMissing++;

                logger.Warning(Log.Line($"Compat: {fileName} skipped. {reason}"));
            }
        }

        var message =
            $"Applied {report.Rules} compat rule(s) from {report.Files} file(s). " +
            $"SkippedFiles={skippedFiles}, TargetsFound={report.TargetsFound}, " +
            $"TargetsMissing={report.TargetsMissing}, FiltersTouched={report.FiltersTouched}, " +
            $"Added={report.Added}, Replaced={report.Replaced}, " +
            $"MissingAllowedTemplates={report.MissingAllowedTemplates}, " +
            $"SpecialSlotsMatched={report.SpecialSlotsMatched}, SpecialSlotsPatched={report.SpecialSlotsPatched}, " +
            $"Warnings={warnings}.";

        logger.Info(Log.Line($"Compat: {message}"));

        return ModuleResult.Ok("Compat", message);
    }

    private static void CountMissingAllowedTemplates(
        Dictionary<MongoId, SPTarkov.Server.Core.Models.Eft.Common.Tables.TemplateItem> items,
        CompatRule rule,
        CompatReport report)
    {
        foreach (var tpl in rule.AllowedTpls)
        {
            if (!items.ContainsKey(new MongoId(tpl)))
            {
                report.MissingAllowedTemplates++;
            }
        }
    }
}
