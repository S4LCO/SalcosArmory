using System.Collections;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.Repairing;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace SalcosArmory.Client.FieldRepair;

internal sealed class FieldArmorRepairPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(ItemContextInteractionsSwitcher).GetMethod(
            nameof(ItemContextInteractionsSwitcher.IsActive),
            new[] { typeof(EItemInfoButton) });
    }

    [PatchPostfix]
    private static void Postfix(
        ItemContextInteractionsSwitcher __instance,
        EItemInfoButton button,
        ref bool __result)
    {
        if (!__result
            && button == EItemInfoButton.Repair
            && FieldArmorRepair.CanEnableRepair(__instance))
        {
            __result = true;
        }
    }
}

internal sealed class FieldArmorRepairersPatch : ModulePatch
{
    private static readonly FieldInfo TradersField = AccessTools.Field(
        typeof(ArmorRepairStrategy),
        "_traders");

    private static readonly FieldInfo DefaultTradersField = AccessTools.Field(
        typeof(DefaultRepairStrategy),
        "_traders");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(
            typeof(RepairerParametersPanel),
            nameof(RepairerParametersPanel.Show),
            new[]
            {
                typeof(IRepairStrategy),
                typeof(RepairController),
                typeof(Item),
                typeof(Inventory),
                typeof(RepairKit)
            });
    }

    [PatchPrefix]
    private static void Prefix(
        IRepairStrategy repairStrategy,
        RepairController repairController,
        RepairKit draggedRepairKit)
    {
        if (!FieldArmorRepair.IsFieldKit(draggedRepairKit)
            || repairStrategy == null
            || repairController == null)
        {
            return;
        }

        // The vanilla window collects traders and every compatible kit. A field repair
        // initiated by dragging FARK must use only the physical kit carried into the raid.
        var collections = repairStrategy.RepairKitsCollections;
        RepairKitsCollection fieldRepairer = null;

        if (collections != null)
        {
            foreach (var collection in collections)
            {
                if (string.Equals(
                        collection.RepairerId,
                        draggedRepairKit.TemplateId.ToString(),
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    fieldRepairer = collection;
                    break;
                }
            }

            collections.Clear();
            fieldRepairer ??= repairController.CreateRepairKitsCollection(draggedRepairKit);
            fieldRepairer.AddRepairKit(draggedRepairKit);
            collections.Add(fieldRepairer);
            repairStrategy.CurrentRepairer = fieldRepairer;
        }

        var traders = repairStrategy is ArmorRepairStrategy
            ? TradersField?.GetValue(repairStrategy) as IList
            : DefaultTradersField?.GetValue(repairStrategy) as IList;

        traders?.Clear();
    }
}
