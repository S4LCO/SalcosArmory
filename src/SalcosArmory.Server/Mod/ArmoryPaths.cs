namespace SalcosArmory.Mod;

public sealed record ArmoryPaths(
    string Root,
    string Config,
    string Database,
    string Bundles,
    string Resources,
    string CustomItems,
    string CustomWeaponPresets,
    string CustomRecipes,
    string CustomLocales,
    string CustomBuffs,
    string CompatItems,
    string CompatWeapons
)
{
    public static ArmoryPaths FromAssembly(Assembly assembly)
    {
        var root = Path.GetDirectoryName(assembly.Location);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException("Could not resolve the mod folder.");
        }

        var config = Path.Combine(root, "config");
        var db = Path.Combine(root, "db");

        return new ArmoryPaths(
            Root: root,
            Config: config,
            Database: db,
            Bundles: Path.Combine(root, "bundles"),
            Resources: Path.Combine(root, "res"),
            CustomItems: Path.Combine(db, "CustomItems"),
            CustomWeaponPresets: Path.Combine(db, "CustomWeaponPresets"),
            CustomRecipes: Path.Combine(db, "CustomHideoutRecipes"),
            CustomLocales: Path.Combine(db, "CustomLocales"),
            CustomBuffs: Path.Combine(db, "CustomBuffs"),
            CompatItems: Path.Combine(config, "compat", "items"),
            CompatWeapons: Path.Combine(config, "compat", "weapons")
        );
    }

    public string SettingsFile => Path.Combine(Config, "settings.json");
    public string CountermeasureData => Path.Combine(Root, "data", "countermeasure_protocol");

    public string CountermeasureProtocolFile
    {
        get
        {
            var jsoncFile = Path.Combine(Config, "countermeasure_protocol.jsonc");
            return File.Exists(jsoncFile)
                ? jsoncFile
                : Path.Combine(Config, "countermeasure_protocol.json");
        }
    }

    public string RuntimeInjectionFile
    {
        get
        {
            var jsoncFile = Path.Combine(Config, "runtime_injection.jsonc");
            return File.Exists(jsoncFile)
                ? jsoncFile
                : Path.Combine(Config, "runtime_injection.json");
        }
    }

    public string WaylandBaseFile => Path.Combine(Database, "traders", "wayland", "base.json");
    public string WaylandPortraitFile => Path.Combine(Resources, "wayland.png");

    public string WaylandConfigFile
    {
        get
        {
            var jsoncFile = Path.Combine(Config, "wayland.jsonc");
            return File.Exists(jsoncFile)
                ? jsoncFile
                : Path.Combine(Config, "wayland.json");
        }
    }
}
