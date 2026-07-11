using SalcosArmory.Config;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Services;

namespace SalcosArmory.Countermeasures;

[Injectable(InjectionType.Singleton)]
public sealed class CountermeasureInventoryService(
    DatabaseService databaseService,
    ItemBaseClassService itemBaseClassService,
    ItemHelper itemHelper,
    CountermeasureStateStore stateStore)
{
    private static readonly MongoId[] OpticBaseClasses =
    [
        BaseClasses.ASSAULT_SCOPE,
        BaseClasses.OPTIC_SCOPE,
        BaseClasses.SPECIAL_SCOPE,
        BaseClasses.COLLIMATOR,
        BaseClasses.COMPACT_COLLIMATOR
    ];

    private static readonly MongoId[] IntermediateBaseClasses =
    [
        BaseClasses.MOUNT,
        BaseClasses.GEAR_MOD,
        BaseClasses.AUXILIARY_MOD
    ];

    private readonly object _cacheSync = new();
    private readonly Dictionary<MongoId, IReadOnlyList<MongoId>> _baseClassCandidates = [];
    private readonly Dictionary<MongoId, IReadOnlyList<MongoId>> _filterCandidates = [];
    private readonly Dictionary<string, IReadOnlyList<MongoId>> _slotCandidates =
        new(StringComparer.Ordinal);

    private CountermeasureProtocolSettings _settings = CountermeasureProtocolSettings.Default;
    private bool _enabled;

    public void Configure(CountermeasureProtocolSettings settings)
    {
        lock (_cacheSync)
        {
            _settings = settings;
            _enabled = true;
            _baseClassCandidates.Clear();
            _filterCandidates.Clear();
            _slotCandidates.Clear();
        }
    }

    public CountermeasureApplicationReport Apply(
        MongoId sessionId,
        BotBaseInventory? inventory,
        BotGenerationDetails generation)
    {
        if (!_enabled
            || inventory?.Items is null
            || inventory.Items.Count == 0
            || !generation.IsPmc
            || generation.IsPlayerScav)
        {
            return CountermeasureApplicationReport.SkippedResult;
        }

        var analysis = stateStore.GetAnalysis(sessionId);
        if (!analysis.IsActive || Random.Shared.NextDouble() >= analysis.AffectedChance)
        {
            return CountermeasureApplicationReport.SkippedResult;
        }

        var attempted = 0;
        var applied = new List<CountermeasureKind>();
        var shuffled = analysis.ActiveCountermeasures
            .OrderBy(_ => Random.Shared.Next())
            .ToArray();

        foreach (var kind in shuffled)
        {
            if (applied.Count >= analysis.CountermeasuresPerBot)
            {
                break;
            }

            attempted++;
            if (TryApply(inventory, kind))
            {
                applied.Add(kind);
            }
        }

        return new CountermeasureApplicationReport(
            false,
            true,
            attempted,
            applied.Count,
            applied
        );
    }

    private bool TryApply(BotBaseInventory inventory, CountermeasureKind kind)
    {
        return kind switch
        {
            CountermeasureKind.NightVision => TryAddAttachmentToEquippedTree(
                inventory,
                "Headwear",
                [BaseClasses.NIGHT_VISION],
                kind),
            CountermeasureKind.FaceProtection => TryAddAttachmentToEquippedTree(
                inventory,
                "Headwear",
                [BaseClasses.VISORS],
                kind),
            CountermeasureKind.LongRangeOptic => TryAddOptic(inventory),
            CountermeasureKind.HearingProtection => TryAddRootEquipment(
                inventory,
                "Earpiece",
                [BaseClasses.HEADPHONES],
                kind),
            CountermeasureKind.ArmorPiercingAmmo => TryUpgradeAmmo(inventory),
            _ => false
        };
    }

    private bool TryAddOptic(BotBaseInventory inventory)
    {
        return TryAddAttachmentToEquippedTree(inventory, "FirstPrimaryWeapon", OpticBaseClasses, CountermeasureKind.LongRangeOptic)
            || TryAddAttachmentToEquippedTree(inventory, "SecondPrimaryWeapon", OpticBaseClasses, CountermeasureKind.LongRangeOptic);
    }

    private bool TryAddRootEquipment(
        BotBaseInventory inventory,
        string slotName,
        IReadOnlyCollection<MongoId> desiredBaseClasses,
        CountermeasureKind kind)
    {
        var equipmentRoot = GetEquipmentRoot(inventory);
        if (equipmentRoot is null || HasChildInSlot(inventory, equipmentRoot, slotName))
        {
            return false;
        }

        if (!databaseService.GetItems().TryGetValue(equipmentRoot.Template, out var template))
        {
            return false;
        }

        var slot = template.Properties?.Slots?.FirstOrDefault(candidate =>
            string.Equals(candidate?.Name, slotName, StringComparison.OrdinalIgnoreCase));

        if (slot is null)
        {
            return false;
        }

        var candidate = SelectFinalCandidate(inventory, slot, desiredBaseClasses, kind);
        return candidate is not null && AddItem(inventory, equipmentRoot, slot.Name!, candidate.Value) is not null;
    }

    private bool TryAddAttachmentToEquippedTree(
        BotBaseInventory inventory,
        string equipmentSlot,
        IReadOnlyCollection<MongoId> desiredBaseClasses,
        CountermeasureKind kind)
    {
        var root = GetEquippedRoot(inventory, equipmentSlot);
        if (root is null)
        {
            return false;
        }

        var tree = GetItemTree(inventory, root).ToArray();
        if (tree.Any(item => HasAnyBaseClass(item.Template, desiredBaseClasses)))
        {
            return false;
        }

        foreach (var host in tree.OrderBy(_ => Random.Shared.Next()))
        {
            if (!databaseService.GetItems().TryGetValue(host.Template, out var hostTemplate))
            {
                continue;
            }

            var slots = hostTemplate.Properties?.Slots ?? [];
            foreach (var slot in slots.OrderBy(_ => Random.Shared.Next()))
            {
                if (slot?.Name is null || HasChildInSlot(inventory, host, slot.Name))
                {
                    continue;
                }

                var path = FindAttachmentPath(
                    inventory,
                    slot,
                    desiredBaseClasses,
                    kind,
                    _settings.MaximumAttachmentDepth,
                    []);

                if (path is null || !AddPath(inventory, host, path))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private IReadOnlyList<AttachmentStep>? FindAttachmentPath(
        BotBaseInventory inventory,
        Slot slot,
        IReadOnlyCollection<MongoId> desiredBaseClasses,
        CountermeasureKind kind,
        int depth,
        HashSet<MongoId> visited)
    {
        if (depth <= 0 || string.IsNullOrWhiteSpace(slot.Name))
        {
            return null;
        }

        var final = SelectFinalCandidate(inventory, slot, desiredBaseClasses, kind);
        if (final is not null)
        {
            return [new AttachmentStep(slot.Name, final.Value)];
        }

        if (depth == 1)
        {
            return null;
        }

        var intermediates = ResolveSlotCandidates(slot)
            .Where(candidate => !visited.Contains(candidate))
            .Where(candidate => HasAnyBaseClass(candidate, IntermediateBaseClasses))
            .Where(candidate => !HasConflict(inventory, candidate))
            .Where(candidate => databaseService.GetItems().TryGetValue(candidate, out var template)
                && template.Properties?.Slots?.Any() == true)
            .OrderBy(_ => Random.Shared.Next())
            .Take(16)
            .ToArray();

        foreach (var intermediate in intermediates)
        {
            if (!databaseService.GetItems().TryGetValue(intermediate, out var template))
            {
                continue;
            }

            var nextVisited = new HashSet<MongoId>(visited) { intermediate };
            foreach (var childSlot in template.Properties?.Slots ?? [])
            {
                if (childSlot is null)
                {
                    continue;
                }

                var tail = FindAttachmentPath(
                    inventory,
                    childSlot,
                    desiredBaseClasses,
                    kind,
                    depth - 1,
                    nextVisited);

                if (tail is null)
                {
                    continue;
                }

                return [new AttachmentStep(slot.Name, intermediate), .. tail];
            }
        }

        return null;
    }

    private MongoId? SelectFinalCandidate(
        BotBaseInventory inventory,
        Slot slot,
        IReadOnlyCollection<MongoId> desiredBaseClasses,
        CountermeasureKind kind)
    {
        var candidates = ResolveSlotCandidates(slot)
            .Where(candidate => HasAnyBaseClass(candidate, desiredBaseClasses))
            .Where(candidate => !HasConflict(inventory, candidate))
            .OrderByDescending(candidate => Score(candidate, kind))
            .Take(4)
            .ToArray();

        return candidates.Length == 0
            ? (MongoId?)null
            : candidates[Random.Shared.Next(candidates.Length)];
    }

    private bool TryUpgradeAmmo(BotBaseInventory inventory)
    {
        var templates = databaseService.GetItems();
        var ammoItems = inventory.Items!
            .Where(item => itemBaseClassService.ItemHasBaseClass(item.Template, BaseClasses.AMMO))
            .ToArray();

        if (ammoItems.Length == 0)
        {
            return false;
        }

        var replacements = 0;
        foreach (var group in ammoItems.GroupBy(item => item.Template))
        {
            if (!templates.TryGetValue(group.Key, out var currentTemplate)
                || currentTemplate.Properties?.PenetrationPower is not int currentPenetration
                || string.IsNullOrWhiteSpace(currentTemplate.Properties.AmmoCaliber))
            {
                continue;
            }

            var maximumPenetration = Math.Min(
                currentPenetration + _settings.AmmoPenetrationIncrease,
                _settings.AmmoPenetrationCap);

            var upgrade = GetCandidatesForBaseClass(BaseClasses.AMMO)
                .Where(candidate => templates.TryGetValue(candidate, out var template)
                    && string.Equals(
                        template.Properties?.AmmoCaliber,
                        currentTemplate.Properties.AmmoCaliber,
                        StringComparison.OrdinalIgnoreCase)
                    && template.Properties?.PenetrationPower > currentPenetration
                    && template.Properties.PenetrationPower <= maximumPenetration)
                .Where(candidate => group.All(item => IsAmmoCompatibleWithParent(inventory, item, candidate)))
                .OrderByDescending(candidate => templates[candidate].Properties?.PenetrationPower ?? 0)
                .FirstOrDefault();

            if (upgrade == default)
            {
                continue;
            }

            foreach (var item in group)
            {
                item.Template = upgrade;
                replacements++;
            }
        }

        return replacements > 0;
    }

    private bool IsAmmoCompatibleWithParent(BotBaseInventory inventory, Item ammo, MongoId candidate)
    {
        if (string.IsNullOrWhiteSpace(ammo.ParentId))
        {
            return true;
        }

        var parent = inventory.Items!.FirstOrDefault(item =>
            string.Equals(item.Id.ToString(), ammo.ParentId, StringComparison.Ordinal));
        if (parent is null || !databaseService.GetItems().TryGetValue(parent.Template, out var parentTemplate))
        {
            return true;
        }

        var slots = (parentTemplate.Properties?.Cartridges ?? [])
            .Concat(parentTemplate.Properties?.Chambers ?? [])
            .Where(slot => slot?.Name is not null)
            .ToArray();

        if (slots.Length == 0)
        {
            return true;
        }

        var matchingSlot = slots.FirstOrDefault(slot =>
            string.Equals(slot!.Name, ammo.SlotId, StringComparison.OrdinalIgnoreCase));

        return matchingSlot is null || SlotAllows(matchingSlot, candidate);
    }

    private bool AddPath(BotBaseInventory inventory, Item host, IReadOnlyList<AttachmentStep> path)
    {
        var parent = host;
        var addedIds = new List<MongoId>();
        foreach (var step in path)
        {
            if (HasConflict(inventory, step.Template))
            {
                RollBackAddedItems(inventory, addedIds);
                return false;
            }

            var added = AddItem(inventory, parent, step.SlotName, step.Template);
            if (added is null)
            {
                RollBackAddedItems(inventory, addedIds);
                return false;
            }

            addedIds.Add(added.Id);
            parent = added;
        }

        return true;
    }

    private Item? AddItem(BotBaseInventory inventory, Item parent, string slotName, MongoId template)
    {
        if (inventory.Items!.Any(item =>
                string.Equals(item.ParentId, parent.Id.ToString(), StringComparison.Ordinal)
                && string.Equals(item.SlotId, slotName, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!databaseService.GetItems().TryGetValue(template, out var itemTemplate))
        {
            return null;
        }

        var upd = itemHelper.GenerateUpdForItem(itemTemplate);
        if (HasAnyBaseClass(template, [BaseClasses.NIGHT_VISION, BaseClasses.VISORS]))
        {
            upd ??= new Upd();
            upd.Togglable = new UpdTogglable { On = true };
        }

        if (HasAnyBaseClass(template, [BaseClasses.VISORS]))
        {
            upd ??= new Upd();
            upd.FaceShield ??= new UpdFaceShield { Hits = 0 };
        }

        var item = new Item
        {
            Id = new MongoId(),
            Template = template,
            ParentId = parent.Id.ToString(),
            SlotId = slotName,
            Location = null,
            Upd = upd
        };

        inventory.Items!.Add(item);
        return item;
    }

    private static void RollBackAddedItems(BotBaseInventory inventory, IReadOnlyCollection<MongoId> addedIds)
    {
        if (addedIds.Count == 0)
        {
            return;
        }

        var ids = addedIds.Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal);
        inventory.Items!.RemoveAll(item => addedIds.Contains(item.Id) || ids.Contains(item.ParentId ?? string.Empty));
    }

    private Item? GetEquipmentRoot(BotBaseInventory inventory)
    {
        return inventory.Equipment is null
            ? null
            : inventory.Items!.FirstOrDefault(item => item.Id == inventory.Equipment.Value);
    }

    private Item? GetEquippedRoot(BotBaseInventory inventory, string slotName)
    {
        var equipmentRoot = GetEquipmentRoot(inventory);
        return equipmentRoot is null
            ? null
            : inventory.Items!.FirstOrDefault(item =>
                string.Equals(item.ParentId, equipmentRoot.Id.ToString(), StringComparison.Ordinal)
                && string.Equals(item.SlotId, slotName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasChildInSlot(BotBaseInventory inventory, Item parent, string slotName)
    {
        return inventory.Items!.Any(item =>
            string.Equals(item.ParentId, parent.Id.ToString(), StringComparison.Ordinal)
            && string.Equals(item.SlotId, slotName, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Item> GetItemTree(BotBaseInventory inventory, Item root)
    {
        var result = new List<Item>();
        var queue = new Queue<Item>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            foreach (var child in inventory.Items!.Where(item =>
                         string.Equals(item.ParentId, current.Id.ToString(), StringComparison.Ordinal)))
            {
                queue.Enqueue(child);
            }
        }

        return result;
    }

    private IReadOnlyList<MongoId> ResolveSlotCandidates(Slot slot)
    {
        var filterIds = slot.Properties?.Filters?
            .Where(filter => filter?.Filter is not null)
            .SelectMany(filter => filter!.Filter!)
            .Distinct()
            .OrderBy(id => id.ToString(), StringComparer.Ordinal)
            .ToArray() ?? [];

        if (filterIds.Length == 0)
        {
            return [];
        }

        var key = string.Join('|', filterIds);
        lock (_cacheSync)
        {
            if (_slotCandidates.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var resolved = new HashSet<MongoId>();
            foreach (var filterId in filterIds)
            {
                foreach (var candidate in ResolveFilterCandidates(filterId))
                {
                    resolved.Add(candidate);
                }
            }

            var result = resolved.ToArray();
            _slotCandidates[key] = result;
            return result;
        }
    }

    private IReadOnlyList<MongoId> ResolveFilterCandidates(MongoId filterId)
    {
        if (_filterCandidates.TryGetValue(filterId, out var cached))
        {
            return cached;
        }

        var items = databaseService.GetItems();
        IReadOnlyList<MongoId> result;

        if (items.TryGetValue(filterId, out var direct)
            && string.Equals(direct.Type, "Item", StringComparison.OrdinalIgnoreCase))
        {
            result = [filterId];
        }
        else
        {
            result = items
                .Where(pair => string.Equals(pair.Value.Type, "Item", StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .Where(candidate => itemBaseClassService.ItemHasBaseClass(candidate, filterId))
                .ToArray();
        }

        _filterCandidates[filterId] = result;
        return result;
    }

    private IReadOnlyList<MongoId> GetCandidatesForBaseClass(MongoId baseClass)
    {
        lock (_cacheSync)
        {
            if (_baseClassCandidates.TryGetValue(baseClass, out var cached))
            {
                return cached;
            }

            var result = databaseService.GetItems()
                .Where(pair => string.Equals(pair.Value.Type, "Item", StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .Where(candidate => itemBaseClassService.ItemHasBaseClass(candidate, baseClass))
                .ToArray();

            _baseClassCandidates[baseClass] = result;
            return result;
        }
    }

    private bool SlotAllows(Slot slot, MongoId candidate)
    {
        var filters = slot.Properties?.Filters?.ToArray() ?? [];
        if (filters.Length == 0)
        {
            return true;
        }

        return filters.Any(filter => filter?.Filter?.Any(allowed =>
            allowed == candidate || itemBaseClassService.ItemHasBaseClass(candidate, allowed)) == true);
    }

    private bool HasAnyBaseClass(MongoId candidate, IEnumerable<MongoId> baseClasses)
    {
        return baseClasses.Any(baseClass => itemBaseClassService.ItemHasBaseClass(candidate, baseClass));
    }

    private bool HasConflict(BotBaseInventory inventory, MongoId candidate)
    {
        var templates = databaseService.GetItems();
        if (!templates.TryGetValue(candidate, out var candidateTemplate))
        {
            return true;
        }

        var candidateConflicts = candidateTemplate.Properties?.ConflictingItems ?? [];
        foreach (var existing in inventory.Items!)
        {
            if (candidateConflicts.Contains(existing.Template))
            {
                return true;
            }

            if (templates.TryGetValue(existing.Template, out var existingTemplate)
                && existingTemplate.Properties?.ConflictingItems?.Contains(candidate) == true)
            {
                return true;
            }
        }

        return false;
    }

    private double Score(MongoId candidate, CountermeasureKind kind)
    {
        if (!databaseService.GetItems().TryGetValue(candidate, out var template))
        {
            return 0d;
        }

        return kind switch
        {
            CountermeasureKind.FaceProtection => (template.Properties?.ArmorClass ?? 0) * 100d
                + (template.Properties?.Durability ?? 0d),
            CountermeasureKind.LongRangeOptic =>
                (HasAnyBaseClass(candidate, [BaseClasses.ASSAULT_SCOPE, BaseClasses.OPTIC_SCOPE, BaseClasses.SPECIAL_SCOPE]) ? 1000d : 0d)
                + (template.Properties?.SightingRange ?? 0d),
            CountermeasureKind.NightVision => template.Properties?.Intensity ?? 0d,
            _ => 0d
        };
    }

    private sealed record AttachmentStep(string SlotName, MongoId Template);
}
