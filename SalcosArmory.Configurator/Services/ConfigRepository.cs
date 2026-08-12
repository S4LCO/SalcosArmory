using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SalcosArmory.Configurator.Models;

namespace SalcosArmory.Configurator.Services;

internal sealed class ConfigRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly FileDefinition[] FileDefinitions =
    [
        new(ConfigSection.General, "settings.json", "settings.json"),
        new(ConfigSection.Countermeasures, "countermeasure_protocol.jsonc", "countermeasure_protocol.jsonc", "countermeasure_protocol.json"),
        new(ConfigSection.Wayland, "wayland.jsonc", "wayland.jsonc", "wayland.json"),
        new(ConfigSection.SoftArmor, "soft_armor_balance.jsonc", "soft_armor_balance.jsonc", "soft_armor_balance.json"),
        new(ConfigSection.RuntimeInjection, "runtime_injection.jsonc", "runtime_injection.json", "runtime_injection.json")
    ];

    private readonly Dictionary<ConfigSection, LoadedConfigFile> _files = [];
    private readonly Dictionary<string, LoadedTextFile> _advancedFiles =
        new(StringComparer.OrdinalIgnoreCase);

    public ConfigWorkspace Load(string configDirectory)
    {
        _files.Clear();
        foreach (var definition in FileDefinitions)
        {
            _files[definition.Section] = LoadFile(configDirectory, definition);
        }

        _advancedFiles.Clear();
        var knownPaths = _files.Values
            .Select(file => Path.GetFullPath(file.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in EnumerateConfigFiles(configDirectory).Where(path => !knownPaths.Contains(Path.GetFullPath(path))))
        {
            var relativePath = Path.GetRelativePath(configDirectory, path).Replace('\\', '/');
            var text = File.ReadAllText(path);
            JsoncFileStore.ParseNode(text, relativePath);
            _advancedFiles[relativePath] = new LoadedTextFile(path, text);
        }

        var workspace = new ConfigWorkspace
        {
            ConfigDirectory = configDirectory,
            General = ReadGeneral(_files[ConfigSection.General].Root),
            Countermeasures = ReadCountermeasures(_files[ConfigSection.Countermeasures].Root),
            Wayland = ReadWayland(_files[ConfigSection.Wayland].Root),
            SoftArmor = ReadSoftArmor(_files[ConfigSection.SoftArmor].Root),
            RuntimeInjection = ReadRuntimeInjection(_files[ConfigSection.RuntimeInjection].Root),
            AdvancedFiles = new ObservableCollection<AdvancedConfigFile>(_advancedFiles
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new AdvancedConfigFile
                {
                    RelativePath = pair.Key,
                    Text = pair.Value.Text
                }))
        };

        ApplyWaylandItemNames(workspace.Wayland, configDirectory);

        return workspace;
    }

    public bool HasChanges(ConfigWorkspace workspace)
    {
        return BuildUpdatedRoots(workspace)
                   .Any(pair => !JsonNode.DeepEquals(_files[pair.Key].Root, pair.Value))
               || workspace.AdvancedFiles.Any(file =>
                   _advancedFiles.TryGetValue(file.RelativePath, out var original)
                   && !string.Equals(original.Text, file.Text, StringComparison.Ordinal));
    }

    public SaveResult Save(ConfigWorkspace workspace)
    {
        var updatedRoots = BuildUpdatedRoots(workspace);
        var changedSections = updatedRoots
            .Where(pair => !JsonNode.DeepEquals(_files[pair.Key].Root, pair.Value))
            .Select(pair => pair.Key)
            .ToArray();
        var changedAdvancedFiles = workspace.AdvancedFiles
            .Where(file => _advancedFiles.TryGetValue(file.RelativePath, out var original)
                && !string.Equals(original.Text, file.Text, StringComparison.Ordinal))
            .ToArray();

        if (changedSections.Length == 0 && changedAdvancedFiles.Length == 0)
        {
            return new SaveResult(0, null);
        }

        EnsureFilesWereNotChangedExternally(changedSections);
        EnsureAdvancedFilesWereNotChangedExternally(changedAdvancedFiles);
        foreach (var file in changedAdvancedFiles)
        {
            JsoncFileStore.ParseNode(file.Text, file.RelativePath);
        }

        var backup = CreateBackup(workspace.ConfigDirectory, "before-save");

        foreach (var section in changedSections)
        {
            var file = _files[section];
            var updatedRoot = updatedRoots[section];
            var updatedText = JsoncFileStore.Merge(file.Text, file.Root, updatedRoot);
            JsoncFileStore.WriteAtomically(file.Path, updatedText);
            _files[section] = file with
            {
                Text = updatedText,
                Root = updatedRoot
            };
        }

        foreach (var advancedFile in changedAdvancedFiles)
        {
            var original = _advancedFiles[advancedFile.RelativePath];
            JsoncFileStore.WriteAtomically(original.Path, advancedFile.Text);
            _advancedFiles[advancedFile.RelativePath] = original with { Text = advancedFile.Text };
        }

        return new SaveResult(changedSections.Length + changedAdvancedFiles.Length, backup);
    }

    public void ResetSection(ConfigWorkspace workspace, ConfigSection section)
    {
        var root = LoadDefaultRoot(FileDefinitions.Single(x => x.Section == section));

        switch (section)
        {
            case ConfigSection.General:
                workspace.General = ReadGeneral(root);
                break;
            case ConfigSection.Countermeasures:
                workspace.Countermeasures = ReadCountermeasures(root);
                break;
            case ConfigSection.Wayland:
                workspace.Wayland = ReadWayland(root);
                break;
            case ConfigSection.SoftArmor:
                workspace.SoftArmor = ReadSoftArmor(root);
                break;
            case ConfigSection.RuntimeInjection:
                workspace.RuntimeInjection = ReadRuntimeInjection(root);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
    }

    public void ExportProfile(string destination)
    {
        using var archive = ZipFile.Open(destination, ZipArchiveMode.Create);
        var configDirectory = Path.GetDirectoryName(_files[ConfigSection.General].Path)
            ?? throw new InvalidOperationException("Could not resolve the active config directory.");
        foreach (var path in EnumerateConfigFiles(configDirectory))
        {
            var relativePath = Path.GetRelativePath(configDirectory, path).Replace('\\', '/');
            archive.CreateEntryFromFile(path, relativePath, CompressionLevel.Optimal);
        }
    }

    public ImportResult ImportProfile(string archivePath, string configDirectory)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var imports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in FileDefinitions)
        {
            var entry = archive.Entries.FirstOrDefault(candidate =>
                !candidate.FullName.Replace('\\', '/').Contains('/')
                && definition.AcceptedNames.Contains(Path.GetFileName(candidate.FullName), StringComparer.OrdinalIgnoreCase));
            if (entry is null)
            {
                continue;
            }

            using var reader = new StreamReader(entry.Open());
            var text = reader.ReadToEnd();
            var root = JsoncFileStore.ParseObject(text, entry.FullName);
            ValidateDeserialization(definition.Section, root);
            imports[_files[definition.Section].Path] = text;
        }

        foreach (var entry in archive.Entries.Where(entry =>
                     entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                     || entry.FullName.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase)))
        {
            var relativePath = entry.FullName.Replace('\\', '/').TrimStart('/');
            if (relativePath.Split('/').Any(part => part is "" or "." or ".."))
            {
                continue;
            }

            var isKnownRootFile = !relativePath.Contains('/')
                && FileDefinitions.Any(definition =>
                    definition.AcceptedNames.Contains(relativePath, StringComparer.OrdinalIgnoreCase));
            if (isKnownRootFile)
            {
                continue;
            }

            var destination = Path.GetFullPath(Path.Combine(configDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var configRoot = Path.GetFullPath(configDirectory) + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(configRoot, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var reader = new StreamReader(entry.Open());
            var text = reader.ReadToEnd();
            JsoncFileStore.ParseNode(text, relativePath);
            imports[destination] = text;
        }

        if (imports.Count == 0)
        {
            throw new InvalidDataException("The selected archive contains no SALCO's ARMORY configuration files.");
        }

        var backup = CreateBackup(configDirectory, "before-import");
        foreach (var import in imports)
        {
            JsoncFileStore.WriteAtomically(import.Key, import.Value);
        }

        return new ImportResult(imports.Count, backup);
    }

    private LoadedConfigFile LoadFile(string configDirectory, FileDefinition definition)
    {
        var path = definition.AcceptedNames
            .Select(name => Path.Combine(configDirectory, name))
            .FirstOrDefault(File.Exists)
            ?? Path.Combine(configDirectory, definition.PreferredName);

        var text = File.Exists(path)
            ? File.ReadAllText(path)
            : LoadDefaultText(definition);
        var root = JsoncFileStore.ParseObject(text, Path.GetFileName(path));
        ValidateDeserialization(definition.Section, root);
        return new LoadedConfigFile(path, text, root);
    }

    private Dictionary<ConfigSection, JsonObject> BuildUpdatedRoots(ConfigWorkspace workspace)
    {
        return new Dictionary<ConfigSection, JsonObject>
        {
            [ConfigSection.General] = PreserveUnknownRootFields(
                _files[ConfigSection.General].Root,
                SerializeObject(workspace.General)),
            [ConfigSection.Countermeasures] = PreserveUnknownRootFields(
                _files[ConfigSection.Countermeasures].Root,
                SerializeObject(workspace.Countermeasures)),
            [ConfigSection.Wayland] = PreserveUnknownRootFields(
                _files[ConfigSection.Wayland].Root,
                SerializeObject(ToWaylandDocument(workspace.Wayland))),
            [ConfigSection.SoftArmor] = PreserveUnknownRootFields(
                _files[ConfigSection.SoftArmor].Root,
                SerializeObject(ToSoftArmorDocument(workspace.SoftArmor))),
            [ConfigSection.RuntimeInjection] = PreserveUnknownRootFields(
                _files[ConfigSection.RuntimeInjection].Root,
                SerializeObject(ToRuntimeInjectionDocument(workspace.RuntimeInjection)))
        };
    }

    private static JsonObject PreserveUnknownRootFields(JsonObject original, JsonObject generated)
    {
        var result = (JsonObject)original.DeepClone();
        foreach (var property in generated)
        {
            result[property.Key] = property.Value?.DeepClone();
        }

        return result;
    }

    private static JsonObject SerializeObject<T>(T value)
    {
        return JsonSerializer.SerializeToNode(value, SerializerOptions) as JsonObject
            ?? throw new InvalidOperationException($"Could not serialize {typeof(T).Name}.");
    }

    private static GeneralConfig ReadGeneral(JsonObject root)
    {
        return root.Deserialize<GeneralConfig>(SerializerOptions)
            ?? throw new InvalidDataException("settings.json could not be read.");
    }

    private static CountermeasureConfig ReadCountermeasures(JsonObject root)
    {
        return root.Deserialize<CountermeasureConfig>(SerializerOptions)
            ?? throw new InvalidDataException("countermeasure_protocol.jsonc could not be read.");
    }

    private static WaylandConfig ReadWayland(JsonObject root)
    {
        var document = root.Deserialize<WaylandDocument>(SerializerOptions)
            ?? throw new InvalidDataException("wayland.jsonc could not be read.");

        return new WaylandConfig
        {
            ShowOffersOnFlea = document.ShowOffersOnFlea,
            PriceSource = document.PriceSource,
            PriceMultiplier = document.PriceMultiplier,
            MinimumPrice = document.MinimumPrice,
            RefreshTimeMinMinutes = document.RefreshTimeMinMinutes,
            RefreshTimeMaxMinutes = document.RefreshTimeMaxMinutes,
            Categories = new ObservableCollection<WaylandCategoryRow>(document.Categories.Select(pair => new WaylandCategoryRow
            {
                Name = pair.Key,
                LoyaltyLevel = pair.Value.LoyaltyLevel,
                Stock = pair.Value.Stock
            })),
            ItemOverrides = new ObservableCollection<WaylandItemRow>(document.ItemOverrides.Select(pair => new WaylandItemRow
            {
                TemplateId = pair.Key,
                Enabled = pair.Value.Enabled ?? true,
                LoyaltyLevel = pair.Value.LoyaltyLevel,
                Stock = pair.Value.Stock,
                Price = pair.Value.Price
            }))
        };
    }

    private static SoftArmorConfig ReadSoftArmor(JsonObject root)
    {
        var document = root.Deserialize<SoftArmorDocument>(SerializerOptions)
            ?? throw new InvalidDataException("soft_armor_balance.jsonc could not be read.");

        return new SoftArmorConfig
        {
            Enabled = document.Enabled,
            Classes = new ObservableCollection<SoftArmorClassRow>(document.Classes.Select(pair => new SoftArmorClassRow
            {
                ArmorClass = pair.Key,
                BaseDurability = pair.Value.BaseDurability,
                BluntThroughput = pair.Value.BluntThroughput,
                FullSetWeightKg = pair.Value.FullSetWeightKg,
                FullSetSpeedPenaltyPercent = pair.Value.FullSetSpeedPenaltyPercent,
                FullSetMousePenalty = pair.Value.FullSetMousePenalty,
                FullSetErgonomicPenalty = pair.Value.FullSetErgonomicPenalty,
                RepairCost = pair.Value.RepairCost,
                FrontBackFleaPrice = pair.Value.FrontBackFleaPrice,
                FrontBackHandbookPrice = pair.Value.FrontBackHandbookPrice,
                StaticLootWeight = pair.Value.StaticLootWeight,
                WaylandStock = pair.Value.WaylandStock
            })),
            Positions = new ObservableCollection<SoftArmorPositionRow>(document.Positions.Select(pair => new SoftArmorPositionRow
            {
                Position = pair.Key,
                DurabilityMultiplier = pair.Value.DurabilityMultiplier,
                SetShare = pair.Value.SetShare,
                PriceMultiplier = pair.Value.PriceMultiplier
            }))
        };
    }

    private static RuntimeInjectionConfig ReadRuntimeInjection(JsonObject root)
    {
        var document = root.Deserialize<RuntimeInjectionDocument>(SerializerOptions)
            ?? throw new InvalidDataException("runtime_injection.jsonc could not be read.");

        return new RuntimeInjectionConfig
        {
            ApplyToPlayerScav = document.ApplyToPlayerScav,
            Targets = new ObservableCollection<RuntimeTargetRow>(document.Targets.Select(target => new RuntimeTargetRow
            {
                ItemTpl = target.ItemTpl,
                Slots = new ObservableCollection<RuntimeSlotRow>(target.Slots.Select(pair => new RuntimeSlotRow
                {
                    SlotName = pair.Key,
                    ItemTpl = pair.Value
                }))
            }))
        };
    }

    private static WaylandDocument ToWaylandDocument(WaylandConfig config)
    {
        return new WaylandDocument
        {
            ShowOffersOnFlea = config.ShowOffersOnFlea,
            PriceSource = config.PriceSource,
            PriceMultiplier = config.PriceMultiplier,
            MinimumPrice = config.MinimumPrice,
            RefreshTimeMinMinutes = config.RefreshTimeMinMinutes,
            RefreshTimeMaxMinutes = config.RefreshTimeMaxMinutes,
            Categories = config.Categories.ToDictionary(
                row => row.Name,
                row => new WaylandCategoryDocument
                {
                    LoyaltyLevel = row.LoyaltyLevel,
                    Stock = row.Stock
                },
                StringComparer.OrdinalIgnoreCase),
            ItemOverrides = config.ItemOverrides.ToDictionary(
                row => row.TemplateId,
                row => new WaylandItemDocument
                {
                    Enabled = row.Enabled ? null : false,
                    LoyaltyLevel = row.LoyaltyLevel,
                    Stock = row.Stock,
                    Price = row.Price
                },
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static void ApplyWaylandItemNames(WaylandConfig wayland, string configDirectory)
    {
        var customItemsDirectory = Path.Combine(
            Directory.GetParent(configDirectory)?.FullName ?? configDirectory,
            "db",
            "CustomItems");
        if (!Directory.Exists(customItemsDirectory))
        {
            return;
        }

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(customItemsDirectory, "*.*", SearchOption.AllDirectories)
                     .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                         || path.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var root = JsoncFileStore.ParseObject(File.ReadAllText(path), Path.GetFileName(path));
                foreach (var item in root)
                {
                    if (item.Value is not JsonObject itemObject)
                    {
                        continue;
                    }

                    var name = itemObject["locales"]?["en"]?["name"]?.GetValue<string>()
                        ?? itemObject["overrideProperties"]?["Name"]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names[item.Key] = name;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or InvalidOperationException)
            {
                // A broken item file is reported by the server. It should not block config editing.
            }
        }

        foreach (var item in wayland.ItemOverrides)
        {
            if (names.TryGetValue(item.TemplateId, out var name))
            {
                item.DisplayName = name;
            }
        }
    }

    private static SoftArmorDocument ToSoftArmorDocument(SoftArmorConfig config)
    {
        return new SoftArmorDocument
        {
            Enabled = config.Enabled,
            Classes = config.Classes.ToDictionary(
                row => row.ArmorClass,
                row => new SoftArmorClassDocument
                {
                    BaseDurability = row.BaseDurability,
                    BluntThroughput = row.BluntThroughput,
                    FullSetWeightKg = row.FullSetWeightKg,
                    FullSetSpeedPenaltyPercent = row.FullSetSpeedPenaltyPercent,
                    FullSetMousePenalty = row.FullSetMousePenalty,
                    FullSetErgonomicPenalty = row.FullSetErgonomicPenalty,
                    RepairCost = row.RepairCost,
                    FrontBackFleaPrice = row.FrontBackFleaPrice,
                    FrontBackHandbookPrice = row.FrontBackHandbookPrice,
                    StaticLootWeight = row.StaticLootWeight,
                    WaylandStock = row.WaylandStock
                },
                StringComparer.OrdinalIgnoreCase),
            Positions = config.Positions.ToDictionary(
                row => row.Position,
                row => new SoftArmorPositionDocument
                {
                    DurabilityMultiplier = row.DurabilityMultiplier,
                    SetShare = row.SetShare,
                    PriceMultiplier = row.PriceMultiplier
                },
                StringComparer.OrdinalIgnoreCase)
        };
    }

    private static RuntimeInjectionDocument ToRuntimeInjectionDocument(RuntimeInjectionConfig config)
    {
        return new RuntimeInjectionDocument
        {
            ApplyToPlayerScav = config.ApplyToPlayerScav,
            Targets = config.Targets.Select(target => new RuntimeTargetDocument
            {
                ItemTpl = target.ItemTpl,
                Slots = target.Slots.ToDictionary(
                    slot => slot.SlotName,
                    slot => slot.ItemTpl,
                    StringComparer.OrdinalIgnoreCase)
            }).ToList()
        };
    }

    private static void ValidateDeserialization(ConfigSection section, JsonObject root)
    {
        switch (section)
        {
            case ConfigSection.General:
                _ = ReadGeneral(root);
                break;
            case ConfigSection.Countermeasures:
                _ = ReadCountermeasures(root);
                break;
            case ConfigSection.Wayland:
                _ = ReadWayland(root);
                break;
            case ConfigSection.SoftArmor:
                _ = ReadSoftArmor(root);
                break;
            case ConfigSection.RuntimeInjection:
                _ = ReadRuntimeInjection(root);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section), section, null);
        }
    }

    private void EnsureFilesWereNotChangedExternally(IEnumerable<ConfigSection> sections)
    {
        foreach (var section in sections)
        {
            var file = _files[section];
            if (File.Exists(file.Path)
                && !string.Equals(File.ReadAllText(file.Path), file.Text, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(file.Path)} changed outside the Configurator. Reload before saving.");
            }
        }
    }

    private void EnsureAdvancedFilesWereNotChangedExternally(IEnumerable<AdvancedConfigFile> files)
    {
        foreach (var advancedFile in files)
        {
            var original = _advancedFiles[advancedFile.RelativePath];
            if (!File.Exists(original.Path)
                || !string.Equals(File.ReadAllText(original.Path), original.Text, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{advancedFile.RelativePath} changed outside the Configurator. Reload before saving.");
            }
        }
    }

    private string CreateBackup(string configDirectory, string reason)
    {
        var modDirectory = Directory.GetParent(configDirectory)?.FullName ?? configDirectory;
        var backupDirectory = Path.Combine(modDirectory, "configurator-backups");
        Directory.CreateDirectory(backupDirectory);

        var backupPath = Path.Combine(
            backupDirectory,
            $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{reason}.zip");
        using var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create);

        foreach (var path in EnumerateConfigFiles(configDirectory))
        {
            var relativePath = Path.GetRelativePath(configDirectory, path).Replace('\\', '/');
            archive.CreateEntryFromFile(path, relativePath, CompressionLevel.Optimal);
        }

        return backupPath;
    }

    private static string LoadDefaultText(FileDefinition definition)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(name =>
            name.EndsWith(definition.DefaultResourceName.Replace('\\', '.'), StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded default '{definition.DefaultResourceName}' was not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded default '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static JsonObject LoadDefaultRoot(FileDefinition definition)
    {
        return JsoncFileStore.ParseObject(
            LoadDefaultText(definition),
            $"default {definition.DefaultResourceName}");
    }

    private static IEnumerable<string> EnumerateConfigFiles(string configDirectory)
    {
        if (!Directory.Exists(configDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(configDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record FileDefinition(
        ConfigSection Section,
        string PreferredName,
        string DefaultResourceName,
        params string[] AlternateNames)
    {
        public IReadOnlyList<string> AcceptedNames { get; } = [PreferredName, .. AlternateNames];
    }

    private sealed record LoadedConfigFile(string Path, string Text, JsonObject Root);

    private sealed record LoadedTextFile(string Path, string Text);

    private sealed class WaylandDocument
    {
        public bool ShowOffersOnFlea { get; set; } = true;
        public string PriceSource { get; set; } = "flea";
        public double PriceMultiplier { get; set; } = 1;
        public int MinimumPrice { get; set; } = 1;
        public int RefreshTimeMinMinutes { get; set; } = 60;
        public int RefreshTimeMaxMinutes { get; set; } = 120;
        public Dictionary<string, WaylandCategoryDocument> Categories { get; set; } = [];
        public Dictionary<string, WaylandItemDocument> ItemOverrides { get; set; } = [];
    }

    private sealed class WaylandCategoryDocument
    {
        public int LoyaltyLevel { get; set; } = 1;
        public int Stock { get; set; } = 3;
    }

    private sealed class WaylandItemDocument
    {
        public bool? Enabled { get; set; }
        public int? LoyaltyLevel { get; set; }
        public int? Stock { get; set; }
        public int? Price { get; set; }
    }

    private sealed class SoftArmorDocument
    {
        public bool Enabled { get; set; } = true;
        public Dictionary<string, SoftArmorClassDocument> Classes { get; set; } = [];
        public Dictionary<string, SoftArmorPositionDocument> Positions { get; set; } = [];
    }

    private sealed class SoftArmorClassDocument
    {
        public int BaseDurability { get; set; }
        public double BluntThroughput { get; set; }
        public double FullSetWeightKg { get; set; }
        public double FullSetSpeedPenaltyPercent { get; set; }
        public double FullSetMousePenalty { get; set; }
        public double FullSetErgonomicPenalty { get; set; }
        public int RepairCost { get; set; }
        public int FrontBackFleaPrice { get; set; }
        public int FrontBackHandbookPrice { get; set; }
        public int StaticLootWeight { get; set; }
        public int WaylandStock { get; set; }
    }

    private sealed class SoftArmorPositionDocument
    {
        public double DurabilityMultiplier { get; set; }
        public double SetShare { get; set; }
        public double PriceMultiplier { get; set; }
    }

    private sealed class RuntimeInjectionDocument
    {
        public bool ApplyToPlayerScav { get; set; }
        public List<RuntimeTargetDocument> Targets { get; set; } = [];
    }

    private sealed class RuntimeTargetDocument
    {
        public string ItemTpl { get; set; } = string.Empty;
        public Dictionary<string, string> Slots { get; set; } = [];
    }
}

internal sealed record SaveResult(int ChangedFiles, string? BackupPath);

internal sealed record ImportResult(int ImportedFiles, string BackupPath);
