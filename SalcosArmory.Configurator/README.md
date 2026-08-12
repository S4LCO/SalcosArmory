# SALCO's ARMORY Configurator

Optional Windows configuration utility for SALCO's ARMORY. The mod itself does
not depend on this application.

## Use

Place `SALCOs ARMORY Configurator.exe` in the SPT installation folder and run it
while the SPT server and game are closed. The Configurator automatically finds:

```text
SPT_Runtime/user/mods/SalcosArmory/config
```

The application can also be started from another location. Use **Browse** to
select the SPT folder, `SPT_Runtime`, the `SalcosArmory` mod folder, or its
`config` folder.

Before every save or profile import, all JSON and JSONC files below the active
config folder are archived to:

```text
SalcosArmory/configurator-backups
```

Normal form edits preserve JSONC comments. Additional compatibility files and
future config files remain available through the **Advanced JSONC** tab.

## Build

```powershell
dotnet build SalcosArmory.sln -c Release
dotnet publish SalcosArmory.Configurator/SalcosArmory.Configurator.csproj -c Release -o SalcosArmory.Configurator/publish
```

The publish output is a compressed, self-contained `win-x64` executable. Users
do not need to install a separate .NET desktop runtime.
