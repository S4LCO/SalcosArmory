using System;
using System.Reflection;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace SalcosArmory.Client.Redline;

internal sealed class RedlineMedEffectPatch : ModulePatch
{
    private static PropertyInfo _medItemProperty;
    private static PropertyInfo _healthControllerProperty;
    private static FieldInfo _medItemField;
    private static FieldInfo _healthControllerField;
    private static FieldInfo _interruptedField;

    protected override MethodBase GetTargetMethod()
    {
        var medEffectType = typeof(ActiveHealthController).GetNestedType(
            "MedEffect",
            BindingFlags.Public | BindingFlags.NonPublic
        );

        if (medEffectType == null || !ConfigureAccess(medEffectType))
        {
            throw new MissingMemberException(
                "The expected ActiveHealthController.MedEffect item/controller members were not found."
            );
        }

        // Residue runs only after the medical use finishes successfully. This keeps the
        // custom 45-second buffer aligned with the vanilla stim buff delay and ignores
        // cancelled injections.
        return AccessTools.Method(medEffectType, "Residue")
            ?? throw new MissingMethodException(medEffectType.FullName, "Residue");
    }

    [PatchPostfix]
    private static void Postfix(object __instance)
    {
        try
        {
            if (_interruptedField?.GetValue(__instance) is bool interrupted && interrupted)
            {
                return;
            }

            var item = ReadValue<Item>(__instance, _medItemProperty, _medItemField);
            var controller = ReadValue<ActiveHealthController>(
                __instance,
                _healthControllerProperty,
                _healthControllerField
            );

            RedlineEffect.TryActivate(item, controller);
        }
        catch (Exception ex)
        {
            SalcosArmoryPlugin.Log.LogError($"E.F.-1 REDLINE use detection failed: {ex}");
        }
    }

    private static bool ConfigureAccess(Type medEffectType)
    {
        _medItemProperty = FindProperty(medEffectType, "MedItem");
        _healthControllerProperty = FindProperty(medEffectType, "HealthController");
        _medItemField = FindField(medEffectType, "_medItem", "Item_0");
        _healthControllerField = FindField(
            medEffectType,
            "_healthController",
            "ActiveHealthController_0"
        );
        _interruptedField = FindField(medEffectType, "_interrupted", "Boolean_0");

        return (_medItemProperty != null || _medItemField != null)
            && (_healthControllerProperty != null || _healthControllerField != null)
            && _interruptedField != null;
    }

    private static T ReadValue<T>(object instance, PropertyInfo property, FieldInfo field)
        where T : class
    {
        return property?.GetValue(instance) as T ?? field?.GetValue(instance) as T;
    }

    private static PropertyInfo FindProperty(Type type, string name)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var property = current.GetProperty(
                name,
                BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly
            );

            if (property != null)
            {
                return property;
            }
        }

        return null;
    }

    private static FieldInfo FindField(Type type, params string[] names)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            foreach (var name in names)
            {
                var field = current.GetField(
                    name,
                    BindingFlags.Instance
                        | BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.DeclaredOnly
                );

                if (field != null)
                {
                    return field;
                }
            }
        }

        return null;
    }
}
