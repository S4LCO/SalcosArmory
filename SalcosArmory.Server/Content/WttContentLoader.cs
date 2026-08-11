using SalcosArmory.Buffs;
using SalcosArmory.Config;
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Content;

[Injectable(InjectionType.Singleton)]
public sealed class WttContentLoader(
    WTTServerCommonLib.WTTServerCommonLib wtt,
    StimBuffService stimBuffService,
    SoftArmorBalanceService softArmorBalanceService,
    TemplateTable templateTable,
    ISptLogger<WttContentLoader> logger
)
{
    private static readonly MongoId[] ContentBackportProbeTemplates =
    [
        new("69413241b1ce1e5fbb09ed0a"), // CENS ProFlex DX5
        new("68916701bd91cc17c109c758"), // UNIT-12 20-round magazine
        new("68935269af943a06e8038c58"), // UNIT-12 30-round magazine
        new("689166debd91cc17c109c753")  // UNIT-12 receiver
    ];

    public static IReadOnlySet<MongoId> ContentBackportDependentItemIds { get; } =
        new HashSet<MongoId>
    {
        new("6a48120af1dbac3e19696b02"), // SALCO CENS ProFlex DX5
        new("6a46c432c8845ba02b25ef73"), // SALCO UNIT-12 20-round magazine
        new("6a46c45d5926a117f3dec591"), // SALCO UNIT-12 30-round magazine
        new("6a492ea31a8326f9d8e55b87"), // SALCO UNIT-12
        new("6a496016abdc368b201e318d")  // SALCO 6B45 armored rig
    };

    private bool _hasDeferredContentBackportItems;
    private int _deferredContentBackportConfigCount;

    public bool HasDeferredContentBackportItems => _hasDeferredContentBackportItems;

    public async Task<ModuleResult> LoadAsync(Assembly assembly, ArmoryPaths paths, ArmorySettings settings)
    {
        var loaded = 0;
        var skipped = 0;
        var contentBackportDetected = AreContentBackportTemplatesAvailable();
        _deferredContentBackportConfigCount = settings.LoadItems
            ? Files.EnumerateJson(paths.CustomItems).Count(ContainsContentBackportDependentItem)
            : 0;
        _hasDeferredContentBackportItems = !contentBackportDetected
            && _deferredContentBackportConfigCount > 0;

        if (_hasDeferredContentBackportItems)
        {
            logger.Info(Log.Line(
                $"Deferring {_deferredContentBackportConfigCount} Content Backport-dependent item config(s) " +
                "until Content Backport has registered its templates."
            ));
        }

        loaded += await Run("Custom items", settings.LoadItems, paths.CustomItems,
            () => LoadCustomItems(
                assembly,
                paths,
                contentBackportDetected
                    ? CustomItemSelection.All
                    : CustomItemSelection.IndependentOnly));

        loaded += await Run("Weapon presets", settings.LoadWeaponPresets, paths.CustomWeaponPresets,
            () => wtt.CustomWeaponPresetService.CreateCustomWeaponPresets(assembly));

        loaded += await Run("Hideout recipes", settings.LoadHideoutRecipes, paths.CustomRecipes,
            () => wtt.CustomHideoutRecipeService.CreateHideoutRecipes(assembly));

        loaded += await Run("Locales", settings.LoadLocales, paths.CustomLocales,
            () => wtt.CustomLocaleService.CreateCustomLocales(assembly));

        var buffs = await LoadBuffs();
        if (!buffs.Success)
        {
            return ModuleResult.Failed("Content", buffs.Message);
        }

        if (buffs.IsSkipped)
        {
            skipped++;
        }
        else
        {
            loaded++;
        }

        return loaded == 0
            ? ModuleResult.Skipped("Content", "No enabled content folders had JSON files.")
            : ModuleResult.Ok(
                "Content",
                $"Registered {loaded} content folder(s). " +
                $"Skipped {skipped} empty/disabled folder(s). " +
                $"ContentBackport={(contentBackportDetected ? "detected" : _hasDeferredContentBackportItems ? "deferred" : "not required")}."
            );

        async Task<int> Run(string name, bool enabled, string folder, Func<Task> action)
        {
            if (!enabled)
            {
                skipped++;
                logger.Info(Log.Line($"{name}: disabled."));
                return 0;
            }

            if (!Files.HasJson(folder))
            {
                skipped++;
                logger.Info(Log.Line($"{name}: no JSON files."));
                return 0;
            }

            await action();
            logger.Info(Log.Line($"{name}: registered."));
            return 1;
        }

        async Task<ModuleResult> LoadBuffs()
        {
            if (!settings.LoadBuffs)
            {
                logger.Info(Log.Line("Buffs: disabled."));
                return ModuleResult.Skipped("Buffs", "Disabled in settings.");
            }

            var result = await stimBuffService.LoadAsync(assembly, paths);

            if (result.IsSkipped)
            {
                logger.Info(Log.Line($"Buffs: {result.Message}"));
            }
            else if (result.Success)
            {
                logger.Info(Log.Line($"Buffs: {result.Message}"));
            }

            return result;
        }
    }

    public async Task<ModuleResult> LoadDeferredContentBackportAsync(
        Assembly assembly,
        ArmoryPaths paths,
        ArmorySettings settings)
    {
        const string moduleName = "Content Backport integration";

        if (!_hasDeferredContentBackportItems)
        {
            return ModuleResult.Skipped(moduleName, "No dependent item configs are waiting to be loaded.");
        }

        if (!settings.LoadItems)
        {
            _hasDeferredContentBackportItems = false;
            return ModuleResult.Skipped(moduleName, "Custom items are disabled in settings.");
        }

        if (!AreContentBackportTemplatesAvailable())
        {
            _hasDeferredContentBackportItems = false;
            logger.Warning(Log.Line(
                "WTT Content Backport templates were not detected after the dependency load phase. " +
                "Backport-dependent items remain unavailable; all independent content remains loaded."
            ));
            return ModuleResult.Skipped(
                moduleName,
                $"Content Backport is unavailable; skipped {_deferredContentBackportConfigCount} dependent item config(s).");
        }

        await LoadCustomItems(assembly, paths, CustomItemSelection.ContentBackportOnly);
        _hasDeferredContentBackportItems = false;

        return ModuleResult.Ok(
            moduleName,
            $"Registered {_deferredContentBackportConfigCount} deferred item config(s) after Content Backport.");
    }

    private async Task LoadCustomItems(
        Assembly assembly,
        ArmoryPaths paths,
        CustomItemSelection selection)
    {
        var stagingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"SalcosArmory-items-{Environment.ProcessId}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(stagingDirectory);

        try
        {
            var copied = 0;
            var skipped = 0;
            var balanced = 0;

            foreach (var file in Files.EnumerateJson(paths.CustomItems))
            {
                var dependsOnContentBackport = ContainsContentBackportDependentItem(file);
                if (selection == CustomItemSelection.IndependentOnly && dependsOnContentBackport
                    || selection == CustomItemSelection.ContentBackportOnly && !dependsOnContentBackport)
                {
                    skipped++;
                    continue;
                }

                var relativePath = Path.GetRelativePath(paths.CustomItems, file).Replace('\\', '/');
                var destination = Path.Combine(
                    stagingDirectory,
                    $"{copied:D4}_{Path.GetFileName(file)}");

                if (softArmorBalanceService.TryTransform(file, relativePath, out var transformedJson))
                {
                    await File.WriteAllTextAsync(destination, transformedJson);
                    balanced++;
                }
                else
                {
                    File.Copy(file, destination, overwrite: true);
                }

                copied++;
            }

            if (copied == 0)
            {
                logger.Info(Log.Line($"Custom item phase {selection}: no matching JSON files."));
                return;
            }

            if (selection == CustomItemSelection.IndependentOnly && skipped > 0)
            {
                logger.Info(Log.Line(
                    $"Content Backport staging deferred {skipped} dependent item config(s); " +
                    $"loading {copied} independent config(s)."));
            }
            else if (selection == CustomItemSelection.ContentBackportOnly)
            {
                logger.Info(Log.Line(
                    $"Content Backport staging is loading {copied} deferred item config(s)."));
            }

            if (softArmorBalanceService.Enabled)
            {
                logger.Info(Log.Line(
                    $"Soft armor insert rebalance transformed {balanced} item config(s)."));
            }

            await wtt.CustomItemServiceExtended.CreateCustomItems(assembly, stagingDirectory);
        }
        finally
        {
            try
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                logger.Warning(Log.Line(
                    $"Temporary custom-item staging directory could not be removed: {ex.Message}"));
            }
        }
    }

    private bool AreContentBackportTemplatesAvailable()
    {
        return ContentBackportProbeTemplates.All(templateTable.Items.ContainsKey);
    }

    private static bool ContainsContentBackportDependentItem(string file)
    {
        try
        {
            using var document = JsonDocument.Parse(
                File.ReadAllText(file),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });

            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.EnumerateObject().Any(property =>
                    ContentBackportDependentItemIds.Contains(new MongoId(property.Name)));
        }
        catch (JsonException)
        {
            // Let CommonLib report malformed independent files through its normal validation path.
            return false;
        }
    }

    private enum CustomItemSelection
    {
        All,
        IndependentOnly,
        ContentBackportOnly
    }
}
