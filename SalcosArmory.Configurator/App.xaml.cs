using System.Windows;

namespace SalcosArmory.Configurator;

public partial class App : Application
{
    public static IReadOnlyList<string> StartupArguments { get; private set; } = [];

    protected override void OnStartup(StartupEventArgs e)
    {
        StartupArguments = e.Args;
        base.OnStartup(e);
    }
}
