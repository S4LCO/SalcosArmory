using System.Globalization;
using System.Text.Json;
using SPTarkov.DI.Annotations;

namespace SalcosArmory.Buffs;

[Injectable(InjectionType.Singleton)]
public sealed class StimBuffValidator
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    public async Task<StimBuffValidationResult> ValidateAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var raw = await File.ReadAllTextAsync(filePath);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return StimBuffValidationResult.Failed($"'{fileName}' is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(raw, JsonOptions);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return StimBuffValidationResult.Failed(
                    $"'{fileName}' must use WTT buff format: {{ \"stimTpl\": [ ... ] }}."
                );
            }

            var stimKeys = 0;
            var buffEntries = 0;

            foreach (var stim in document.RootElement.EnumerateObject().OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                var stimTpl = stim.Name.Trim();

                if (string.IsNullOrWhiteSpace(stimTpl))
                {
                    return StimBuffValidationResult.Failed($"'{fileName}' contains an empty stim tpl key.");
                }

                if (stim.Value.ValueKind != JsonValueKind.Array)
                {
                    return StimBuffValidationResult.Failed(
                        $"Stim '{stimTpl}' in '{fileName}' must contain an array of buff entries."
                    );
                }

                if (stim.Value.GetArrayLength() == 0)
                {
                    return StimBuffValidationResult.Failed(
                        $"Stim '{stimTpl}' in '{fileName}' has no buff entries."
                    );
                }

                stimKeys++;

                var index = 0;
                foreach (var buff in stim.Value.EnumerateArray())
                {
                    index++;

                    var error = ValidateBuff(fileName, stimTpl, index, buff);
                    if (!string.IsNullOrEmpty(error))
                    {
                        return StimBuffValidationResult.Failed(error);
                    }

                    buffEntries++;
                }
            }

            if (stimKeys == 0)
            {
                return StimBuffValidationResult.Failed($"'{fileName}' does not contain any stim buff definitions.");
            }

            return StimBuffValidationResult.Ok(stimKeys, buffEntries);
        }
        catch (JsonException ex)
        {
            return StimBuffValidationResult.Failed($"JSON error in '{fileName}': {ex.Message}");
        }
    }

    private static string ValidateBuff(string fileName, string stimTpl, int index, JsonElement buff)
    {
        if (buff.ValueKind != JsonValueKind.Object)
        {
            return $"Buff #{index} for stim '{stimTpl}' in '{fileName}' must be an object.";
        }

        if (!TryGetString(buff, "BuffType", out var buffType) || string.IsNullOrWhiteSpace(buffType))
        {
            return $"Buff #{index} for stim '{stimTpl}' in '{fileName}' has an empty BuffType.";
        }

        if (!TryGetOptionalNumber(buff, "Delay", out var delay, out var delayError))
        {
            return $"Buff #{index} for stim '{stimTpl}' in '{fileName}' has invalid Delay. {delayError}";
        }

        if (delay is < 0)
        {
            return $"Buff #{index} for stim '{stimTpl}' in '{fileName}' has negative Delay.";
        }

        if (!TryGetOptionalNumber(buff, "Duration", out var duration, out var durationError))
        {
            return $"Buff #{index} for stim '{stimTpl}' in '{fileName}' has invalid Duration. {durationError}";
        }

        if (duration is < 0)
        {
            return $"Buff #{index} for stim '{stimTpl}' in '{fileName}' has negative Duration.";
        }

        if (!TryGetOptionalNumber(buff, "Chance", out var chance, out var chanceError))
        {
            return $"Buff #{index} for stim '{stimTpl}' in '{fileName}' has invalid Chance. {chanceError}";
        }

        if (chance is < 0 or > 1)
        {
            return $"Buff #{index} for stim '{stimTpl}' in '{fileName}' has Chance outside 0..1.";
        }

        return string.Empty;
    }

    private static bool TryGetString(JsonElement source, string propertyName, out string value)
    {
        value = string.Empty;

        foreach (var property in source.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = property.Value.GetString() ?? string.Empty;
            return true;
        }

        return false;
    }

    private static bool TryGetOptionalNumber(
        JsonElement source,
        string propertyName,
        out double? value,
        out string error)
    {
        value = null;
        error = string.Empty;

        foreach (var property in source.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return true;
            }

            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var number))
            {
                value = number;
                return true;
            }

            if (property.Value.ValueKind == JsonValueKind.String)
            {
                var raw = property.Value.GetString();

                if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                {
                    value = parsed;
                    return true;
                }
            }

            error = $"Expected a number for '{propertyName}'.";
            return false;
        }

        return true;
    }
}

public sealed record StimBuffValidationResult(
    bool Success,
    int StimKeys,
    int BuffEntries,
    string Message
)
{
    public static StimBuffValidationResult Ok(int stimKeys, int buffEntries)
    {
        return new StimBuffValidationResult(true, stimKeys, buffEntries, string.Empty);
    }

    public static StimBuffValidationResult Failed(string message)
    {
        return new StimBuffValidationResult(false, 0, 0, message);
    }
}