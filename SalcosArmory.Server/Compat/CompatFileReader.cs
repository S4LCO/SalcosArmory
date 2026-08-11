using System.Text.Json;
using SPTarkov.DI.Annotations;

namespace SalcosArmory.Compat;

[Injectable(InjectionType.Singleton)]
public sealed class CompatFileReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<CompatRuleSet> ReadAsync(string file)
    {
        var raw = await File.ReadAllTextAsync(file);
        return JsonSerializer.Deserialize<CompatRuleSet>(raw, Options) ?? new CompatRuleSet();
    }
}
