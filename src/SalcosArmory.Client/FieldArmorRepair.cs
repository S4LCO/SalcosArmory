using System;
using System.Linq;
using EFT.InventoryLogic;
using EFT.UI;

namespace SalcosArmory.Client.FieldRepair;

internal static class FieldArmorRepair
{
    internal const string TemplateId = "6a80f1e1d5a1c0de00000001";

    internal static bool CanEnableRepair(ItemContextInteractionsSwitcher switcher)
    {
        if (switcher == null
            || !switcher.Gameplay
            || switcher.BadContextType
            || switcher._item == null
            || switcher._itemController == null
            || !switcher.Examined
            || !(switcher._itemContext is RepairItemContext repairContext)
            || !IsFieldKit(repairContext.RepairKit)
            || !switcher._itemController.Examined(repairContext.RepairKit))
        {
            return false;
        }

        return switcher._item
            .GetItemComponentsInChildren<RepairableComponent>(true)
            .Select(component => component.Item)
            .Any(item => item.GetItemComponent<ArmorComponent>() != null
                         && repairContext.RepairKit.CanRepair(item));
    }

    internal static bool IsFieldKit(RepairKit repairKit)
    {
        return repairKit != null
            && string.Equals(repairKit.TemplateId, TemplateId, StringComparison.OrdinalIgnoreCase)
            && repairKit.Resource > 0f;
    }
}
