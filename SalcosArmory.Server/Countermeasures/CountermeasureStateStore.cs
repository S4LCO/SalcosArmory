using System.Text.Json;
using System.Text.Json.Serialization;
using SalcosArmory.Config;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Utils;

namespace SalcosArmory.Countermeasures;

[Injectable(InjectionType.Singleton)]
public sealed class CountermeasureStateStore(ISptLogger<CountermeasureStateStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly object _sync = new();
    private readonly Dictionary<string, CountermeasureProfileState> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    private string? _dataFolder;
    private CountermeasureProtocolSettings _settings = CountermeasureProtocolSettings.Default;

    public void Configure(string dataFolder, CountermeasureProtocolSettings settings)
    {
        lock (_sync)
        {
            _dataFolder = dataFolder;
            _settings = settings;
            _cache.Clear();
            Directory.CreateDirectory(dataFolder);
        }
    }

    public CountermeasureAnalysis GetAnalysis(MongoId sessionId)
    {
        lock (_sync)
        {
            return CountermeasureAnalyzer.Analyze(GetOrLoad(sessionId), _settings);
        }
    }

    public CountermeasureAnalysis RecordRaid(MongoId sessionId, CountermeasureRaidRecord raid)
    {
        lock (_sync)
        {
            var state = GetOrLoad(sessionId);
            state.Raids.Add(raid);

            if (state.Raids.Count > _settings.HistorySize)
            {
                state.Raids.RemoveRange(0, state.Raids.Count - _settings.HistorySize);
            }

            state.UpdatedUtc = DateTime.UtcNow;
            Save(sessionId, state);
            return CountermeasureAnalyzer.Analyze(state, _settings);
        }
    }

    private CountermeasureProfileState GetOrLoad(MongoId sessionId)
    {
        var key = sessionId.ToString();
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var state = Load(sessionId);
        _cache[key] = state;
        return state;
    }

    private CountermeasureProfileState Load(MongoId sessionId)
    {
        var path = GetPath(sessionId);
        if (path is null || !File.Exists(path))
        {
            return new CountermeasureProfileState();
        }

        try
        {
            var raw = File.ReadAllText(path);
            var state = JsonSerializer.Deserialize<CountermeasureProfileState>(raw, JsonOptions)
                ?? new CountermeasureProfileState();
            state.Raids ??= [];
            return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.Warning(Log.Line($"Countermeasure Protocol state could not be read: {ex.Message}"));
            return new CountermeasureProfileState();
        }
    }

    private void Save(MongoId sessionId, CountermeasureProfileState state)
    {
        var path = GetPath(sessionId);
        if (path is null)
        {
            return;
        }

        var temporaryPath = path + ".tmp";

        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state, JsonOptions));
            File.Move(temporaryPath, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Warning(Log.Line($"Countermeasure Protocol state could not be saved: {ex.Message}"));
        }
    }

    private string? GetPath(MongoId sessionId)
    {
        return string.IsNullOrWhiteSpace(_dataFolder)
            ? null
            : Path.Combine(_dataFolder, $"{sessionId}.json");
    }
}
