using BepInEx;
using BepInEx.Logging;
using SalcosArmory.Client.MedicalMerge;

namespace SalcosArmory.Client;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
internal sealed class MedicalMergePlugin : BaseUnityPlugin
{
    private const string PluginGuid = "com.salco.salcosarmory.client";
    private const string PluginName = "SALCO's ARMORY Client";
    private const string PluginVersion = "0.2.1";

    internal static ManualLogSource Log { get; private set; }

    private void Awake()
    {
        Log = Logger;

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
}
