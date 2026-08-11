using System;
using System.Linq;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;
using UnityEngine.UI;

namespace SalcosArmory.Client.SpecialSlots;

internal sealed class SpecialSlotLayoutPatch : ModulePatch
{
    private const int Columns = 3;

    private static readonly FieldInfo SpecSlotsPanelField = AccessTools.Field(
        typeof(SearchableSlotView),
        "_specSlotsPanel");

    private static bool _layoutFailureLogged;

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(
            typeof(SearchableSlotView),
            "CreateSlots",
            new[] { typeof(Item) });
    }

    [PatchPostfix]
    private static void Postfix(SearchableSlotView __instance, Item item)
    {
        if (!IsExtendedPockets(item))
        {
            return;
        }

        try
        {
            ApplyGrid(__instance);
        }
        catch (Exception ex)
        {
            if (_layoutFailureLogged)
            {
                return;
            }

            _layoutFailureLogged = true;
            SalcosArmoryPlugin.Log.LogError(
                $"Extended Special Slots could not apply the 3x2 layout: {ex}");
        }
    }

    private static bool IsExtendedPockets(Item item)
    {
        return item is CompoundItem compoundItem
            && compoundItem.Slots.Count(slot =>
                slot?.Name?.StartsWith("SpecialSlot", StringComparison.OrdinalIgnoreCase) == true) >= 6;
    }

    private static void ApplyGrid(SearchableSlotView view)
    {
        var panel = SpecSlotsPanelField?.GetValue(view) as Transform;
        if (panel is null || panel is not RectTransform panelRect)
        {
            throw new InvalidOperationException("The Special Slot panel was not found.");
        }

        var grid = panel.GetComponent<GridLayoutGroup>();
        if (grid is null)
        {
            var horizontal = panel.GetComponent<HorizontalLayoutGroup>();
            var padding = horizontal?.padding;
            var spacing = horizontal?.spacing ?? 0f;

            if (horizontal is not null)
            {
                UnityEngine.Object.DestroyImmediate(horizontal);
            }

            grid = panel.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = padding is null
                ? new RectOffset()
                : new RectOffset(padding.left, padding.right, padding.top, padding.bottom);
            grid.spacing = new Vector2(spacing, spacing);
        }

        var oneByOneSize = ItemViewFactory.GetCellPixelSize(new IntVec2(1, 1));
        grid.cellSize = new Vector2(oneByOneSize.X, oneByOneSize.Y);

        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

        var rows = Math.Max(1, (panel.childCount + Columns - 1) / Columns);
        var requiredHeight = grid.padding.top
            + grid.padding.bottom
            + (grid.cellSize.y * rows)
            + (grid.spacing.y * (rows - 1));

        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, requiredHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

        if (panelRect.parent is RectTransform parentRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }
    }
}
