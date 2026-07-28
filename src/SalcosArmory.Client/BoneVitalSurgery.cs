using System;
using System.Reflection;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;

namespace SalcosArmory.Client.VitalSurgery;

internal static class BoneVitalSurgery
{
    internal const string BoneTemplateId = "6a46c6bd54c2760498b84f47";

    [ThreadStatic]
    private static TreatmentContext _activeTreatment;

    private static FieldInfo _medItemField;
    private static FieldInfo _healthControllerField;
    private static FieldInfo _bodyPartField;

    internal static bool RestorePatchReady { get; private set; }

    internal static bool IsBone(Item item)
    {
        return item != null
            && string.Equals(item.TemplateId, BoneTemplateId, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TrySelectDestroyedVitalPart(
        ActiveHealthController controller,
        out EBodyPart bodyPart)
    {
        bodyPart = EBodyPart.Common;

        if (!RestorePatchReady || controller == null || !controller.IsAlive)
        {
            return false;
        }

        if (controller.IsBodyPartDestroyed(EBodyPart.Head))
        {
            bodyPart = EBodyPart.Head;
            return true;
        }

        if (controller.IsBodyPartDestroyed(EBodyPart.Chest))
        {
            bodyPart = EBodyPart.Chest;
            return true;
        }

        return false;
    }

    internal static bool CanPassRestoreGuard(
        ActiveHealthController controller,
        EBodyPart bodyPart)
    {
        if (!IsVitalPart(bodyPart))
        {
            return true;
        }

        if (!RestorePatchReady || controller == null || !controller.IsAlive)
        {
            return false;
        }

        var treatment = _activeTreatment;
        if (treatment == null
            || treatment.Consumed
            || !ReferenceEquals(controller, treatment.Controller)
            || treatment.BodyPart != bodyPart
            || !controller.IsBodyPartDestroyed(bodyPart))
        {
            return false;
        }

        treatment.Consumed = true;
        MedicalMergePlugin.Log.LogInfo(
            $"B.O.N.E. authorized vital surgery for {bodyPart}."
        );
        return true;
    }

    internal static void MarkRestorePatchReady(bool ready)
    {
        RestorePatchReady = ready;

        if (!ready)
        {
            _activeTreatment = null;
        }
    }

    internal static bool ConfigureMedEffectAccess(Type medEffectType)
    {
        _medItemField = AccessTools.Field(medEffectType, "Item_0");
        _healthControllerField = AccessTools.Field(medEffectType, "ActiveHealthController_0");
        _bodyPartField = AccessTools.Field(medEffectType, "EbodyPart_0");

        return _medItemField != null
            && _healthControllerField != null
            && _bodyPartField != null;
    }

    internal static TreatmentContext EnterMedEffect(object medEffect)
    {
        var previousTreatment = _activeTreatment;
        _activeTreatment = null;

        if (medEffect == null
            || !(_medItemField?.GetValue(medEffect) is Item item)
            || !IsBone(item)
            || !(_healthControllerField?.GetValue(medEffect) is ActiveHealthController controller)
            || !(_bodyPartField?.GetValue(medEffect) is EBodyPart bodyPart)
            || !IsVitalPart(bodyPart)
            || !controller.IsAlive
            || !controller.IsBodyPartDestroyed(bodyPart))
        {
            return previousTreatment;
        }

        _activeTreatment = new TreatmentContext(controller, bodyPart);
        return previousTreatment;
    }

    internal static void ExitMedEffect(TreatmentContext previousTreatment)
    {
        _activeTreatment = previousTreatment;
    }

    private static bool IsVitalPart(EBodyPart bodyPart)
    {
        return bodyPart == EBodyPart.Head || bodyPart == EBodyPart.Chest;
    }

    internal sealed class TreatmentContext
    {
        internal TreatmentContext(ActiveHealthController controller, EBodyPart bodyPart)
        {
            Controller = controller;
            BodyPart = bodyPart;
        }

        internal ActiveHealthController Controller { get; }

        internal EBodyPart BodyPart { get; }

        internal bool Consumed { get; set; }
    }
}
