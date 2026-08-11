using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace SalcosArmory.Runtime;

[Injectable(InjectionType.Singleton)]
public sealed class RuntimeItemInjector(
    RuntimeInjectionPlan plan,
    TemplateTable templateTable)
{
    public RuntimeInjectionReport Apply(BotBaseInventory? inventory, bool includePlayerScav, bool isPlayerScav)
    {
        if (inventory?.Items is null || inventory.Items.Count == 0 || plan.IsEmpty)
        {
            return RuntimeInjectionReport.SkippedResult;
        }

        if (isPlayerScav && !includePlayerScav)
        {
            return RuntimeInjectionReport.SkippedResult;
        }

        var templates = templateTable.Items;
        var inventoryItems = inventory.Items;
        var hostsMatched = 0;
        var slotsConsidered = 0;
        var insertsAdded = 0;
        var occupiedSlots = 0;
        var missingSlots = 0;
        var missingTemplates = 0;
        var blockedInserts = 0;

        foreach (var hostItem in inventoryItems.ToList())
        {
            if (!plan.TryGet(hostItem.Template, out var target))
            {
                continue;
            }

            hostsMatched++;
            slotsConsidered += target.Slots.Count;

            if (!templates.TryGetValue(hostItem.Template, out var hostTemplate) || hostTemplate.Properties is null)
            {
                missingTemplates += target.Slots.Count;
                continue;
            }

            foreach (var (slotName, insertTpl) in target.Slots)
            {
                var slot = hostTemplate.Properties.Slots?.FirstOrDefault(candidate =>
                    !string.IsNullOrWhiteSpace(candidate?.Name)
                    && string.Equals(candidate.Name, slotName, StringComparison.OrdinalIgnoreCase));

                if (slot is null)
                {
                    missingSlots++;
                    continue;
                }

                var hostId = hostItem.Id.ToString();
                if (inventoryItems.Any(item =>
                        string.Equals(item.ParentId, hostId, StringComparison.Ordinal)
                        && string.Equals(item.SlotId, slot.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    occupiedSlots++;
                    continue;
                }

                if (!templates.ContainsKey(insertTpl))
                {
                    missingTemplates++;
                    continue;
                }

                if (!ItemSlotRules.Allows(slot, insertTpl))
                {
                    blockedInserts++;
                    continue;
                }

                inventoryItems.Add(new Item
                {
                    Id = new MongoId(),
                    Template = insertTpl,
                    ParentId = hostId,
                    SlotId = slot.Name,
                    Location = null,
                    Upd = null
                });

                insertsAdded++;
            }
        }

        return new RuntimeInjectionReport(
            false,
            hostsMatched,
            slotsConsidered,
            insertsAdded,
            occupiedSlots,
            missingSlots,
            missingTemplates,
            blockedInserts
        );
    }
}
