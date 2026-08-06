using SalcosArmory.Config;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Countermeasures;

[Injectable(InjectionType.Singleton)]
public sealed class RaidTelemetryService(
    ProfileHelper profileHelper,
    TemplateTable templateTable,
    ItemBaseClassService itemBaseClassService,
    CountermeasureStateStore stateStore,
    ISptLogger<RaidTelemetryService> logger)
{
    private readonly object _sync = new();
    private readonly Dictionary<string, RaidStartSnapshot> _starts = new(StringComparer.OrdinalIgnoreCase);

    private CountermeasureProtocolSettings _settings = CountermeasureProtocolSettings.Default;
    private bool _enabled;
    private bool _debugLogging;

    public void Configure(CountermeasureProtocolSettings settings, bool debugLogging)
    {
        lock (_sync)
        {
            _settings = settings;
            _debugLogging = debugLogging || settings.DebugLogging;
            _enabled = true;
            _starts.Clear();
        }
    }

    public void CaptureRaidStart(MongoId sessionId, GetRaidConfigurationRequestData request)
    {
        if (!_enabled || IsScavRaid(request))
        {
            return;
        }

        try
        {
            var profile = profileHelper.GetPmcProfile(sessionId);
            var inventory = profile?.Inventory;
            var snapshot = new RaidStartSnapshot(
                request.Location ?? string.Empty,
                request.IsNightRaid,
                inventory is not null && HasSuppressor(inventory),
                inventory is not null && HasHeavyArmor(inventory, _settings.HeavyArmorClassThreshold)
            );

            lock (_sync)
            {
                _starts[sessionId.ToString()] = snapshot;
            }

            if (_debugLogging)
            {
                logger.Info(Log.Line(
                    $"Countermeasure telemetry start: location={snapshot.Location}, night={snapshot.NightRaid}, " +
                    $"suppressor={snapshot.UsedSuppressor}, heavyArmor={snapshot.UsedHeavyArmor}."
                ));
            }
        }
        catch (Exception ex)
        {
            logger.Warning(Log.Line($"Countermeasure telemetry start was skipped: {ex.Message}"));
        }
    }

    public void CaptureRaidEnd(MongoId sessionId, EndLocalRaidRequestData request)
    {
        if (!_enabled || request.Results?.Profile?.Stats?.Eft is null)
        {
            return;
        }

        RaidStartSnapshot? start;
        lock (_sync)
        {
            _starts.Remove(sessionId.ToString(), out start);
        }

        if (start is null || request.Results.IsMapToMapTransfer())
        {
            return;
        }

        try
        {
            var victims = request.Results.Profile.Stats.Eft.Victims?.ToArray() ?? [];
            var headshots = victims.Count(IsHeadshot);
            var totalDistance = victims.Sum(victim => Math.Max(0d, victim.Distance ?? 0d));

            var raid = new CountermeasureRaidRecord
            {
                CompletedUtc = DateTime.UtcNow,
                Location = start.Location,
                NightRaid = start.NightRaid,
                UsedSuppressor = start.UsedSuppressor,
                UsedHeavyArmor = start.UsedHeavyArmor,
                Survived = request.Results.IsPlayerSurvived(),
                Kills = victims.Length,
                HeadshotKills = headshots,
                TotalKillDistance = totalDistance
            };

            var analysis = stateStore.RecordRaid(sessionId, raid);
            logger.Info(Log.Line(
                $"Countermeasure telemetry: raids={analysis.RaidCount}, kills={raid.Kills}, " +
                $"headshots={raid.HeadshotKills}, survived={raid.Survived}, active={analysis.IsActive}, " +
                $"affected={analysis.AffectedChance:P0}."
            ));
        }
        catch (Exception ex)
        {
            logger.Warning(Log.Line($"Countermeasure telemetry end was skipped: {ex.Message}"));
        }
    }

    private bool HasSuppressor(BotBaseInventory inventory)
    {
        return GetEquippedTree(inventory, "FirstPrimaryWeapon", "SecondPrimaryWeapon", "Holster")
            .Any(item => itemBaseClassService.ItemHasBaseClass(item.Template, BaseClasses.SILENCER));
    }

    private bool HasHeavyArmor(BotBaseInventory inventory, int armorClassThreshold)
    {
        var templates = templateTable.Items;
        return GetEquippedTree(inventory, "ArmorVest", "TacticalVest")
            .Any(item => IsWornArmorComponent(item)
                && templates.TryGetValue(item.Template, out var template)
                && (template.Properties?.ArmorClass ?? 0) >= armorClassThreshold);
    }

    private static bool IsWornArmorComponent(Item item)
    {
        if (item.SlotId is null)
        {
            return false;
        }

        return item.SlotId.Equals("ArmorVest", StringComparison.OrdinalIgnoreCase)
            || item.SlotId.Equals("TacticalVest", StringComparison.OrdinalIgnoreCase)
            || item.SlotId.Contains("plate", StringComparison.OrdinalIgnoreCase)
            || item.SlotId.Contains("soft_armor", StringComparison.OrdinalIgnoreCase)
            || item.SlotId.Contains("armor", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<Item> GetEquippedTree(BotBaseInventory inventory, params string[] rootSlots)
    {
        if (inventory.Items is null || inventory.Equipment is null)
        {
            return [];
        }

        var wantedSlots = new HashSet<string>(rootSlots, StringComparer.OrdinalIgnoreCase);
        var equipmentId = inventory.Equipment.Value.ToString();
        var roots = inventory.Items
            .Where(item => string.Equals(item.ParentId, equipmentId, StringComparison.Ordinal)
                && item.SlotId is not null
                && wantedSlots.Contains(item.SlotId))
            .ToArray();

        if (roots.Length == 0)
        {
            return [];
        }

        var byParent = inventory.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.ParentId))
            .GroupBy(item => item.ParentId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        var result = new List<Item>();
        var queue = new Queue<Item>(roots);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);

            if (!byParent.TryGetValue(current.Id.ToString(), out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                queue.Enqueue(child);
            }
        }

        return result;
    }

    private static bool IsHeadshot(Victim victim)
    {
        return ContainsHead(victim.BodyPart) || ContainsHead(victim.ColliderType);
    }

    private static bool ContainsHead(string? value)
    {
        return value?.Contains("head", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsScavRaid(GetRaidConfigurationRequestData request)
    {
        return request.Side?.ToString().Contains("savage", StringComparison.OrdinalIgnoreCase) == true;
    }

    private sealed record RaidStartSnapshot(
        string Location,
        bool NightRaid,
        bool UsedSuppressor,
        bool UsedHeavyArmor);
}
