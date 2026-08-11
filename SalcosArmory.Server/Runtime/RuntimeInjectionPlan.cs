using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Common;

namespace SalcosArmory.Runtime;

[Injectable(InjectionType.Singleton)]
public sealed class RuntimeInjectionPlan
{
    private readonly Dictionary<MongoId, RuntimeInjectionTargetPlan> _targets = [];

    public bool IsEmpty => _targets.Count == 0;
    public int TargetCount => _targets.Count;
    public int SlotCount => _targets.Values.Sum(target => target.Slots.Count);

    public void Replace(IEnumerable<ResolvedRuntimeInjectionTarget> targets)
    {
        _targets.Clear();

        foreach (var target in targets)
        {
            _targets[target.HostTpl] = new RuntimeInjectionTargetPlan(target.Slots);
        }
    }

    public bool TryGet(MongoId hostTpl, out RuntimeInjectionTargetPlan target)
    {
        return _targets.TryGetValue(hostTpl, out target!);
    }
}

public sealed record RuntimeInjectionTargetPlan(IReadOnlyDictionary<string, MongoId> Slots);
