namespace SalcosArmory.Infrastructure;

public static class Log
{
    public static string Line(string message)
    {
        return $"{ArmoryInfo.LogPrefix} {message}";
    }
}
