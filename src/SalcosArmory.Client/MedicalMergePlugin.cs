using BepInEx;
using BepInEx.Logging;
using EFT.BinarySerialization;
using SalcosArmory.Client.FieldRepair;
using SalcosArmory.Client.MedicalMerge;
using SalcosArmory.Client.Redline;
using SalcosArmory.Client.SpecialSlots;
using SalcosArmory.Client.StimTextures;
using SalcosArmory.Client.VitalSurgery;

namespace SalcosArmory.Client;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
internal sealed class MedicalMergePlugin : BaseUnityPlugin
{
    private const string PluginGuid = "com.salco.salcosarmory";
    private const string PluginName = "SALCO's ARMORY Client";
    private const string PluginVersion = "0.6.1";

    internal static ManualLogSource Log { get; private set; }

    private void Awake()
    {
        Log = Logger;

        EnableStimInHandsTextures();
        EnableBoneVitalSurgery();
        EnableRedline();
        EnableFieldArmorRepair();
        EnableSpecialSlotLayout();

        new ConvertMedicalMergeResultPatch().Enable();
        new MedicalMergeAvailableInteractionsPatch().Enable();
        new MedicalMergeLabelPatch().Enable();
        new MedicalMergeExecutePatch().Enable();
        new MedicalMergeIsActivePatch().Enable();
        new MedicalMergeIsInteractivePatch().Enable();

        if (!BinarySerializationMirrorExtensions._types.Contains(typeof(MedicalMergeDescriptor)))
        {
            BinarySerializationMirrorExtensions._types.Add(typeof(MedicalMergeDescriptor));
        }

        Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
    }

    private void Update()
    {
        RedlineEffect.Update();
    }

    private void OnDestroy()
    {
        RedlineEffect.Shutdown();
    }

    private static void EnableRedline()
    {
        try
        {
            new RedlineMedEffectPatch().Enable();
            Log.LogInfo("E.F.-1 REDLINE temporary maximum-health effect enabled.");
        }
        catch (System.Exception ex)
        {
            Log.LogError($"E.F.-1 REDLINE was disabled: {ex}");
        }
    }

    private static void EnableFieldArmorRepair()
    {
        try
        {
            new FieldArmorRepairPatch().Enable();
            new FieldArmorRepairersPatch().Enable();
            Log.LogInfo("Field Armor Repair Kit raid interaction enabled.");
        }
        catch (System.Exception ex)
        {
            Log.LogError($"Field Armor Repair Kit was disabled: {ex}");
        }
    }

    private static void EnableSpecialSlotLayout()
    {
        try
        {
            new SpecialSlotLayoutPatch().Enable();
            Log.LogInfo("Extended Special Slots 3x2 layout enabled.");
        }
        catch (System.Exception ex)
        {
            Log.LogError($"Extended Special Slots layout was disabled: {ex}");
        }
    }

    private void EnableStimInHandsTextures()
    {
        try
        {
            if (!StimTextureCatalog.Initialize(Info.Location))
            {
                Log.LogWarning(
                    "Custom stimulant hand textures were disabled because no valid textures were loaded."
                );
                return;
            }

            new StimInHandsTexturePatch().Enable();
        }
        catch (System.Exception ex)
        {
            Log.LogError($"Custom stimulant hand textures were disabled: {ex}");
        }
    }

    private static void EnableBoneVitalSurgery()
    {
        BoneVitalSurgery.MarkRestorePatchReady(false);

        try
        {
            new BoneVitalRestorePatch().Enable();

            if (!BoneVitalSurgery.RestorePatchReady)
            {
                return;
            }

            new BoneVitalMedEffectContextPatch().Enable();
            new BoneVitalBodyPartSelectionPatch().Enable();
        }
        catch (System.Exception ex)
        {
            BoneVitalSurgery.MarkRestorePatchReady(false);
            Log.LogError($"B.O.N.E. vital surgery was disabled: {ex}");
        }
    }
}
