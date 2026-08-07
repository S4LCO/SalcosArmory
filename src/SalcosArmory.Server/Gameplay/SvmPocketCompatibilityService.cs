using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace SalcosArmory.Gameplay;

/// <summary>
/// Makes SVM's fixed custom-pocket templates visible before SPT runs InvalidPocketFix.
/// SVM loads after profile migration in SPT 4.1.2 and safely overwrites these bridge
/// templates with its fully configured versions later in the same server startup.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class SvmPocketCompatibilityService(
    TemplateTable templateTable,
    ICloner cloner,
    ISptLogger<SvmPocketCompatibilityService> logger)
{
    private const string ModuleName = "SVM pocket compatibility";
    private const string SvmAssemblyName = "ServerValueModifier";

    private static readonly MongoId DefaultPmcPockets = new("627a4e6b255f7527fb05a0f6");
    private static readonly MongoId DefaultScavPockets = new("557ffd194bdc2d28148b457f");
    private static readonly MongoId SvmPmcPockets = new("a8edfb0bce53d103d3f62b9b");
    private static readonly MongoId SvmScavPockets = new("a8edfb0bce53d103d3f6219b");

    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public ModuleResult Prepare()
    {
        var svmAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
            string.Equals(
                assembly.GetName().Name,
                SvmAssemblyName,
                StringComparison.OrdinalIgnoreCase));

        if (svmAssembly is null)
        {
            return ModuleResult.Skipped(ModuleName, "SVM is not installed; no bridge is needed.");
        }

        try
        {
            if (!TryReadPocketSettings(
                    svmAssembly.Location,
                    out var pmcCustomPockets,
                    out var scavCustomPockets,
                    out var settingsMessage))
            {
                logger.Warning(Log.Line($"{ModuleName}: {settingsMessage}"));
                return ModuleResult.Skipped(ModuleName, settingsMessage);
            }

            if (!pmcCustomPockets && !scavCustomPockets)
            {
                return ModuleResult.Skipped(
                    ModuleName,
                    "SVM is installed, but custom PMC and Scav pockets are disabled.");
            }

            var prepared = 0;
            if (pmcCustomPockets)
            {
                prepared += PrepareTemplate(DefaultPmcPockets, SvmPmcPockets) ? 1 : 0;
            }

            if (scavCustomPockets)
            {
                prepared += PrepareTemplate(DefaultScavPockets, SvmScavPockets) ? 1 : 0;
            }

            var enabledKinds = string.Join(
                " and ",
                new[]
                {
                    pmcCustomPockets ? "PMC" : null,
                    scavCustomPockets ? "Scav" : null
                }.Where(value => value is not null));

            return ModuleResult.Ok(
                ModuleName,
                prepared == 0
                    ? $"SVM {enabledKinds} pocket bridge already exists."
                    : $"Prepared {prepared} SVM {enabledKinds} pocket template(s) before profile migration.");
        }
        catch (Exception ex)
        {
            // Fail open: SVM and vanilla pocket behavior must remain untouched if its
            // configuration format or template IDs ever change.
            logger.Warning(Log.Line($"{ModuleName} was skipped: {ex.Message}"));
            return ModuleResult.Skipped(ModuleName, ex.Message);
        }
    }

    private bool PrepareTemplate(MongoId sourceId, MongoId targetId)
    {
        if (templateTable.Items.ContainsKey(targetId))
        {
            return false;
        }

        if (!templateTable.Items.TryGetValue(sourceId, out var source))
        {
            throw new InvalidOperationException(
                $"Vanilla source pocket template {sourceId} was not found.");
        }

        var bridge = cloner.Clone(source)
            ?? throw new InvalidOperationException(
                $"SVM pocket bridge could not clone vanilla template {sourceId}.");
        bridge.Id = targetId;
        templateTable.Items[targetId] = bridge;
        return true;
    }

    private static bool TryReadPocketSettings(
        string assemblyPath,
        out bool pmcCustomPockets,
        out bool scavCustomPockets,
        out string message)
    {
        pmcCustomPockets = false;
        scavCustomPockets = false;
        message = string.Empty;

        var modFolder = Path.GetDirectoryName(assemblyPath);
        if (string.IsNullOrWhiteSpace(modFolder))
        {
            message = "SVM's installation folder could not be resolved.";
            return false;
        }

        var loaderPath = Path.Combine(modFolder, "Loader", "loader.json");
        if (!File.Exists(loaderPath))
        {
            message = "SVM's Loader/loader.json was not found; SVM appears to be disabled.";
            return false;
        }

        using var loader = JsonDocument.Parse(File.ReadAllText(loaderPath), JsonOptions);
        if (!TryGetString(loader.RootElement, "CurrentlySelectedPreset", out var selectedPreset)
            || string.IsNullOrWhiteSpace(selectedPreset))
        {
            message = "SVM has no selected preset; its pocket bridge is not required.";
            return false;
        }

        var presetFileName = selectedPreset.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? selectedPreset
            : $"{selectedPreset}.json";

        if (!string.Equals(
                Path.GetFileName(presetFileName),
                presetFileName,
                StringComparison.Ordinal))
        {
            message = "SVM's selected preset contains an invalid file name.";
            return false;
        }

        var presetPath = Path.Combine(modFolder, "Presets", presetFileName);
        if (!File.Exists(presetPath))
        {
            message = $"SVM's selected preset '{presetFileName}' was not found.";
            return false;
        }

        using var preset = JsonDocument.Parse(File.ReadAllText(presetPath), JsonOptions);
        pmcCustomPockets = ReadSectionFlags(
            preset.RootElement,
            "CSM",
            "EnableCSM",
            "CustomPocket");
        scavCustomPockets = ReadSectionFlags(
            preset.RootElement,
            "Scav",
            "EnableScav",
            "ScavCustomPockets");

        return true;
    }

    private static bool ReadSectionFlags(
        JsonElement root,
        string sectionName,
        params string[] flagNames)
    {
        if (!TryGetProperty(root, sectionName, out var section)
            || section.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return flagNames.All(flag =>
            TryGetProperty(section, flag, out var value)
            && value.ValueKind is JsonValueKind.True);
    }

    private static bool TryGetString(JsonElement source, string name, out string value)
    {
        value = string.Empty;
        if (!TryGetProperty(source, name, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool TryGetProperty(JsonElement source, string name, out JsonElement value)
    {
        foreach (var property in source.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
