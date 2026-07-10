using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using InventoryInteractions = GClass3757;

namespace SalcosArmory.Client.MedicalMerge;

internal static class MedicalMergeContext
{
    public const EItemInfoButton Button = EItemInfoButton.TopUp;
    public const string Label = "Merge";

    private static readonly FieldInfo ControllerField =
        AccessTools.Field(typeof(ItemUiContext), "traderControllerClass");

    private static readonly FieldInfo InventoryField =
        AccessTools.GetDeclaredFields(typeof(ItemUiContext))
            .FirstOrDefault(field => field.FieldType == typeof(Inventory));

    public static bool IsAvailable(InventoryInteractions interactions)
    {
        return TryGet(interactions, out _, out _, out var sources) && sources.Count > 0;
    }

    public static bool IsAvailable(ItemUiContext context, MedsItemClass target)
    {
        return TryGet(context, target, out _, out _, out var sources) && sources.Count > 0;
    }

    public static bool TryGet(
        InventoryInteractions interactions,
        out TraderControllerClass itemController,
        out MedsItemClass target,
        out List<MedsItemClass> sources)
    {
        itemController = null;
        target = null;
        sources = new List<MedsItemClass>();

        if (interactions?.Item_0 is not MedsItemClass targetItem)
        {
            return false;
        }

        return TryGet(
            interactions.ItemUiContext_1,
            targetItem,
            out itemController,
            out target,
            out sources
        );
    }

    public static bool TryGet(
        ItemUiContext context,
        MedsItemClass target,
        out TraderControllerClass itemController,
        out MedsItemClass targetItem,
        out List<MedsItemClass> sources)
    {
        itemController = null;
        targetItem = null;
        sources = new List<MedsItemClass>();

        if (context == null
            || target?.MedKitComponent == null
            || target.MedKitComponent.HpResource >= target.MedKitComponent.MaxHpResource)
        {
            return false;
        }

        if (ControllerField?.GetValue(context) is not TraderControllerClass controller
            || InventoryField?.GetValue(context) is not Inventory inventory)
        {
            MedicalMergePlugin.Log.LogWarning("Medical merge is unavailable because the inventory context could not be resolved.");
            return false;
        }

        itemController = controller;
        targetItem = target;
        sources = inventory.GetPlayerItems()
            .OfType<MedsItemClass>()
            .Where(item => item.Id != target.Id)
            .Where(item => item.TemplateId == target.TemplateId)
            .Where(item => item.MedKitComponent != null)
            .Where(item => item.MedKitComponent.HpResource > 0f)
            .Where(item => item.MedKitComponent.HpResource < item.MedKitComponent.MaxHpResource)
            .OrderBy(item => item.MedKitComponent.HpResource)
            .ToList();

        return sources.Count > 0;
    }

    public static void Execute(InventoryInteractions interactions)
    {
        if (!TryGet(interactions, out var itemController, out var target, out var sources))
        {
            return;
        }

        var source = sources.FirstOrDefault();
        if (source == null || !MedicalMergeInteraction.CanMerge(source, target, out _))
        {
            return;
        }

        var result = MedicalMergeInteraction.TryMerge(source, target, 0f, itemController, true);
        if (result.Failed)
        {
            MedicalMergePlugin.Log.LogWarning($"Medical merge could not start: {result.Error}");
            return;
        }

        if (result.Value.TransferAmount > 0f)
        {
            itemController.RunNetworkTransaction(result.Value, null);
        }
    }
}
