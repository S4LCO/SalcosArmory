using BepInEx;
using BepInEx.Logging;
using SalcosArmory.Client.MedicalMerge;
using SalcosArmory.Client.StimTextures;
using SalcosArmory.Client.VitalSurgery;

namespace SalcosArmory.Client;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
internal sealed class MedicalMergePlugin : BaseUnityPlugin
{
    private const string PluginGuid = "com.salco.salcosarmory";
    private const string PluginName = "SALCO's ARMORY Client";
    private const string PluginVersion = "0.4.1";

    internal static ManualLogSource Log { get; private set; }

    private void Awake()
    {
        Log = Logger;

        EnableStimInHandsTextures();
        EnableBoneVitalSurgery();

        new ConvertMedicalMergeResultPatch().Enable();
        new MedicalMergeAvailableInteractionsPatch().Enable();
        new MedicalMergeLabelPatch().Enable();
        new MedicalMergeExecutePatch().Enable();
        new MedicalMergeIsActivePatch().Enable();
        new MedicalMergeIsInteractivePatch().Enable();

        if (!GClass3695.List_0.Contains(typeof(MedicalMergeDescriptor)))
        {
            GClass3695.List_0.Add(typeof(MedicalMergeDescriptor));
        }

        Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
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
