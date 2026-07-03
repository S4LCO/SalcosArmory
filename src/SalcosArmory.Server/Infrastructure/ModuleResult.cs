namespace SalcosArmory.Infrastructure;

public sealed class ModuleResult
{
    public string Name { get; init; } = string.Empty;
    public bool Success { get; init; }
    public bool IsSkipped { get; init; }
    public string Message { get; init; } = string.Empty;

    public static ModuleResult Ok(string name, string message)
    {
        return new ModuleResult
        {
            Name = name,
            Success = true,
            Message = message
        };
    }

    public static ModuleResult Skipped(string name, string message)
    {
        return new ModuleResult
        {
            Name = name,
            Success = true,
            IsSkipped = true,
            Message = message
        };
    }

    public static ModuleResult Failed(string name, string message)
    {
        return new ModuleResult
        {
            Name = name,
            Success = false,
            Message = message
        };
    }
}
