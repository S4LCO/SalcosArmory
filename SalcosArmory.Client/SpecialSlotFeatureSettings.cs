using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace SalcosArmory.Client.SpecialSlots;

internal static class SpecialSlotFeatureSettings
{
    private const string SettingName = "loadExtendedSpecialSlots";

    internal static bool Enabled
    {
        get
        {
            var settingsFile = Path.Combine(
                BepInEx.Paths.GameRootPath,
                "SPT_Runtime",
                "user",
                "mods",
                "SalcosArmory",
                "config",
                "settings.json"
            );

            if (!File.Exists(settingsFile))
            {
                return true;
            }

            try
            {
                var settings = JObject.Parse(File.ReadAllText(settingsFile));
                return settings.Value<bool?>(SettingName) ?? true;
            }
            catch (Exception exception)
            {
                SalcosArmoryPlugin.Log.LogWarning(
                    $"Could not read the Extended Special Slots setting; keeping the layout enabled: {exception.Message}"
                );
                return true;
            }
        }
    }
}
