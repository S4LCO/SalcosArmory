using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SalcosArmory.Config;
using SalcosArmory.Content;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;

namespace SalcosArmory.Traders;

[Injectable(InjectionType.Singleton)]
public sealed class WaylandTraderService(
    ModHelper modHelper,
    ImageRouter imageRouter,
    TraderConfig traderConfig,
    RagfairConfig ragfairConfig,
    TemplateTable templateTable,
    TradersTable tradersTable,
    LocaleTable localeTable,
    SoftArmorBalanceService softArmorBalanceService,
    ISptLogger<WaylandTraderService> logger
)
{
    public const string TraderId = "6a46d0015741594c414e4401";
    private const string MechanicTraderId = "5a7c2eca46aef81a7ca2145d";
    private const string RoubleTpl = "5449016a4bdc2d6f028b456f";
    private const string ModuleName = "Wayland trader";

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private bool _registered;

    public ModuleResult Load(
        ArmoryPaths paths,
        WaylandSettings settings,
        IReadOnlySet<MongoId>? expectedMissingTemplates = null)
    {
        try
        {
            if (!File.Exists(paths.WaylandBaseFile))
            {
                return ModuleResult.Failed(ModuleName, $"Trader base file not found: {paths.WaylandBaseFile}");
            }

            if (!File.Exists(paths.WaylandPortraitFile))
            {
                return ModuleResult.Failed(ModuleName, $"Trader portrait not found: {paths.WaylandPortraitFile}");
            }

            var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(
                paths.Root,
                "db/traders/wayland/base.json");

            if (traderBase.Id.ToString() != TraderId)
            {
                return ModuleResult.Failed(
                    ModuleName,
                    $"Trader base uses GUID {traderBase.Id}; expected {TraderId}.");
            }

            var assort = BuildAssort(paths, settings, expectedMissingTemplates);
            if (assort.Items.Count == 0)
            {
                return ModuleResult.Failed(ModuleName, "No valid SALCO item could be added to the assortment.");
            }

            if (!_registered)
            {
                RegisterPortrait(traderBase, paths.WaylandPortraitFile);
                RegisterRefreshTime(traderBase.Id, settings);
                RegisterLocales(traderBase);
            }

            InheritMechanicBuyRules(traderBase);
            RegisterTrader(traderBase, assort, settings.ShowOffersOnFlea);

            var levels = assort.LoyalLevelItems.Values
                .GroupBy(level => level)
                .OrderBy(group => group.Key)
                .Select(group => $"LL{group.Key}: {group.Count()}");

            var action = _registered ? "Refreshed" : "Registered";
            _registered = true;

            return ModuleResult.Ok(
                ModuleName,
                $"{action} Wayland with {assort.Items.Count} offer(s) ({string.Join(", ", levels)}).");
        }
        catch (Exception ex)
        {
            logger.Error(Log.Line($"Wayland registration failed: {ex}"));
            return ModuleResult.Failed(ModuleName, ex.Message);
        }
    }

    private TraderAssort BuildAssort(
        ArmoryPaths paths,
        WaylandSettings settings,
        IReadOnlySet<MongoId>? expectedMissingTemplates)
    {
        var assort = new TraderAssort
        {
            Items = [],
            BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
            LoyalLevelItems = new Dictionary<MongoId, int>()
        };

        var knownTemplates = templateTable.Items;
        var seenTemplates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ReadCatalog(paths.CustomItems).OrderBy(entry => entry.RelativePath))
        {
            if (!seenTemplates.Add(entry.TemplateId))
            {
                logger.Warning(Log.Line($"Wayland skipped duplicate item template {entry.TemplateId} ({entry.RelativePath})."));
                continue;
            }

            MongoId templateId = entry.TemplateId;
            if (!knownTemplates.ContainsKey(templateId))
            {
                if (expectedMissingTemplates?.Contains(templateId) != true)
                {
                    logger.Warning(Log.Line(
                        $"Wayland skipped unknown item template {entry.TemplateId} ({entry.RelativePath})."));
                }

                continue;
            }

            var category = GetCategory(settings, entry.Category);
            var itemOverride = GetOverride(settings, entry.TemplateId);
            if (itemOverride is { Enabled: false })
            {
                continue;
            }

            var loyaltyLevel = ResolveLoyaltyLevel(entry, category, itemOverride);
            var stock = Math.Max(1, itemOverride?.Stock ?? entry.BalancedStock ?? category.Stock);
            var price = ResolvePrice(entry, settings, itemOverride);
            MongoId offerId = CreateOfferId(entry.TemplateId);

            assort.Items.Add(new Item
            {
                Id = offerId,
                Template = templateId,
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = new Upd
                {
                    UnlimitedCount = true,
                    StackObjectsCount = 9_999_999,
                    BuyRestrictionMax = stock,
                    BuyRestrictionCurrent = 0
                }
            });

            assort.BarterScheme[offerId] =
            [
                [
                    new BarterScheme
                    {
                        Count = price,
                        Template = RoubleTpl
                    }
                ]
            ];
            assort.LoyalLevelItems[offerId] = loyaltyLevel;
        }

        return assort;
    }

    private IEnumerable<CatalogEntry> ReadCatalog(string customItemsPath)
    {
        foreach (var file in Files.EnumerateJson(customItemsPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file), JsonOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var relativePath = System.IO.Path.GetRelativePath(customItemsPath, file).Replace('\\', '/');
            var category = relativePath.Split('/', 2)[0];

            foreach (var itemProperty in document.RootElement.EnumerateObject())
            {
                if (itemProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var handbookPrice = ReadNumber(itemProperty.Value, "handbookPriceRoubles");
                var fleaPrice = ReadNumber(itemProperty.Value, "fleaPriceRoubles");
                int? balancedStock = null;

                if (softArmorBalanceService.TryResolveOffer(
                        relativePath,
                        out var balancedHandbookPrice,
                        out var balancedFleaPrice,
                        out var waylandStock))
                {
                    handbookPrice = balancedHandbookPrice;
                    fleaPrice = balancedFleaPrice;
                    balancedStock = waylandStock;
                }

                yield return new CatalogEntry(
                    itemProperty.Name,
                    category,
                    relativePath,
                    handbookPrice,
                    fleaPrice,
                    balancedStock);
            }
        }
    }

    private void RegisterPortrait(TraderBase traderBase, string portraitPath)
    {
        var avatar = traderBase.Avatar
            ?? throw new InvalidOperationException("Wayland's trader base has no avatar route.");
        var extensionIndex = avatar.LastIndexOf('.');
        var route = extensionIndex > 0 ? avatar[..extensionIndex] : avatar;
        imageRouter.AddRoute(route, portraitPath);
    }

    private void RegisterRefreshTime(MongoId traderId, WaylandSettings settings)
    {
        var minMinutes = Math.Max(1, settings.RefreshTimeMinMinutes);
        var maxMinutes = Math.Max(minMinutes, settings.RefreshTimeMaxMinutes);
        traderConfig.UpdateTime.RemoveAll(update => update.TraderId == traderId);
        traderConfig.UpdateTime.Add(new UpdateTime
        {
            Name = "Wayland",
            TraderId = traderId,
            Seconds = new MinMax<int>(minMinutes * 60, maxMinutes * 60)
        });
    }

    private void RegisterTrader(TraderBase traderBase, TraderAssort assort, bool showOffersOnFlea)
    {
        var trader = new Trader
        {
            Base = traderBase,
            Assort = assort,
            Dialogue = new Dictionary<string, List<string>?>(),
            QuestAssort = new Dictionary<string, Dictionary<MongoId, MongoId>>
            {
                ["started"] = new(),
                ["success"] = new(),
                ["fail"] = new()
            }
        };

        tradersTable[traderBase.Id] = trader;
        ragfairConfig.Traders[traderBase.Id] = showOffersOnFlea;
    }

    private void InheritMechanicBuyRules(TraderBase traderBase)
    {
        MongoId mechanicTraderId = MechanicTraderId;
        if (!tradersTable.TryGetValue(mechanicTraderId, out var mechanic))
        {
            throw new InvalidOperationException("Mechanic could not be found; Wayland's buy rules cannot be initialized.");
        }

        traderBase.ItemsBuy = mechanic.Base.ItemsBuy;
        traderBase.ItemsBuyProhibited = mechanic.Base.ItemsBuyProhibited;
    }

    private void RegisterLocales(TraderBase traderBase)
    {
        var fullName = traderBase.Name;
        var nickname = traderBase.Nickname ?? "Wayland";
        var location = traderBase.Location ?? "The Old Workshop";

        foreach (var locale in localeTable.Global.Values)
        {
            locale.AddTransformer(data =>
            {
                if (data is null)
                {
                    return data;
                }

                data[$"{TraderId} FullName"] = fullName;
                data[$"{TraderId} FirstName"] = "Wayland";
                data[$"{TraderId} Nickname"] = nickname;
                data[$"{TraderId} Location"] = location;
                data[$"{TraderId} Description"] =
                    "A veteran gunsmith and discreet quartermaster who deals exclusively in SALCO's ARMORY equipment.";
                return data;
            });
        }
    }

    private static WaylandCategorySettings GetCategory(WaylandSettings settings, string category)
    {
        var match = settings.Categories.FirstOrDefault(pair =>
            pair.Key.Equals(category, StringComparison.OrdinalIgnoreCase));

        return match.Value ?? new WaylandCategorySettings();
    }

    private static WaylandItemOverride? GetOverride(WaylandSettings settings, string templateId)
    {
        var match = settings.ItemOverrides.FirstOrDefault(pair =>
            pair.Key.Equals(templateId, StringComparison.OrdinalIgnoreCase));

        return match.Value;
    }

    private static int ResolveLoyaltyLevel(
        CatalogEntry entry,
        WaylandCategorySettings category,
        WaylandItemOverride? itemOverride)
    {
        if (itemOverride?.LoyaltyLevel is int overrideLevel)
        {
            return Math.Clamp(overrideLevel, 1, 4);
        }

        for (var level = 3; level <= 6; level++)
        {
            if (entry.RelativePath.Contains($"/Level_{level}/", StringComparison.OrdinalIgnoreCase))
            {
                return level - 2;
            }
        }

        return Math.Clamp(category.LoyaltyLevel, 1, 4);
    }

    private static int ResolvePrice(
        CatalogEntry entry,
        WaylandSettings settings,
        WaylandItemOverride? itemOverride)
    {
        if (itemOverride?.Price is int overridePrice)
        {
            return Math.Max(settings.MinimumPrice, overridePrice);
        }

        var sourcePrice = settings.PriceSource.Equals("handbook", StringComparison.OrdinalIgnoreCase)
            ? entry.HandbookPrice
            : entry.FleaPrice > 0
                ? entry.FleaPrice
                : entry.HandbookPrice;

        var adjusted = (int)Math.Round(sourcePrice * Math.Max(0.01, settings.PriceMultiplier));
        return Math.Max(settings.MinimumPrice, adjusted);
    }

    private static int ReadNumber(JsonElement item, string propertyName)
    {
        return item.TryGetProperty(propertyName, out var value)
               && value.ValueKind == JsonValueKind.Number
               && value.TryGetInt32(out var number)
            ? number
            : 0;
    }

    private static string CreateOfferId(string templateId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{TraderId}:{templateId}"));
        return Convert.ToHexString(hash)[..24].ToLowerInvariant();
    }

    private sealed record CatalogEntry(
        string TemplateId,
        string Category,
        string RelativePath,
        int HandbookPrice,
        int FleaPrice,
        int? BalancedStock);
}
