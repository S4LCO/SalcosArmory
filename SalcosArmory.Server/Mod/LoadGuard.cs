using System.Threading;

namespace SalcosArmory.Mod;

public static class LoadGuard
{
    private static int _loaded;

    public static bool Enter()
    {
        return Interlocked.Exchange(ref _loaded, 1) == 0;
    }
}
