namespace SalcosArmory.Runtime;

public sealed record RuntimeInjectionReport(
    bool Skipped,
    int HostsMatched,
    int SlotsConsidered,
    int InsertsAdded,
    int OccupiedSlots,
    int MissingSlots,
    int MissingTemplates,
    int BlockedInserts
)
{
    public bool HasActivity => HostsMatched > 0 || InsertsAdded > 0;

    public static RuntimeInjectionReport SkippedResult { get; } =
        new(true, 0, 0, 0, 0, 0, 0, 0);
}
