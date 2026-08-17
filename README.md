# SnowFlakeProxy

ASCOM FilterWheel LocalServer that proxies `ASCOM.WandererSnowflakeFilterWheel1.FilterWheel`.

It exists to hide two Wanderer-driver defects:

- Decorated filter names (`Filter 1 (L)`) that SkyGuard cannot enumerate
- A blocking `Position` getter that returns the old slot while the wheel is moving

## Chooser name

**Wanderer Snowflake Filter Wheel 1 (Proxy)**

ProgID: `ASCOM.SnowFlakeProxy.FilterWheel`

## Operating rule

Point NINA, SkyGuard, and every other ASCOM client at this proxy.

Do **not** connect anything directly to `ASCOM.WandererSnowflakeFilterWheel1.FilterWheel` while the proxy is in use. Direct vendor access bypasses serialization and invalidates cached state.

Do **not** put JustAHub in the path.

Do **not** use a name-patched Wanderer executable. The proxy strips `Filter N (name)` itself.

## Build

Visual Studio 2022, .NET Framework 4.7.2, x86 LocalServer. ASCOM Platform 7 is required.

```text
msbuild SnowFlakeProxy.sln /p:Configuration=Release /p:Platform="Any CPU"
```

Register from an elevated Windows PowerShell 5.1 session:

```text
tools\Register-Driver.ps1
tools\Unregister-Driver.ps1
```

## Tests

Unit tests do not need the wheel. Hardware scripts and ConformU do.
