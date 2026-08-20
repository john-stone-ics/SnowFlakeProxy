# SnowFlakeProxy

ASCOM FilterWheel LocalServer that proxies the Wanderer Snowflake Filter Wheel drivers.

It exists to hide two Wanderer-driver defects:

- Decorated filter names (`Filter 1 (L)`) that SkyGuard cannot enumerate
- A blocking `Position` getter that returns the old slot while the wheel is moving

## Chooser names

Wanderer registers three slots so one PC can host up to three Snowflakes. This proxy does the same:

| Chooser name | Proxy ProgID | Wraps |
|---|---|---|
| **Wanderer Snowflake Filter Wheel 1 (Proxy)** | `ASCOM.SnowFlakeProxy1.FilterWheel` | `ASCOM.WandererSnowflakeFilterWheel1.FilterWheel` |
| **Wanderer Snowflake Filter Wheel 2 (Proxy)** | `ASCOM.SnowFlakeProxy2.FilterWheel` | `ASCOM.WandererSnowflakeFilterWheel2.FilterWheel` |
| **Wanderer Snowflake Filter Wheel 3 (Proxy)** | `ASCOM.SnowFlakeProxy3.FilterWheel` | `ASCOM.WandererSnowflakeFilterWheel3.FilterWheel` |

Pick the proxy whose number matches the Wanderer slot that owns that wheel's COM port.

## Operating rule

Point NINA, SkyGuard, and every other ASCOM client at the matching proxy.

Do **not** connect anything directly to the matching `ASCOM.WandererSnowflakeFilterWheelN.FilterWheel` while that proxy is in use. Direct vendor access bypasses serialization and invalidates cached state.

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

Or run `dist\SnowFlakeProxy-0.1.0-Setup.exe`.

## Tests

Unit tests do not need the wheel. Hardware scripts and ConformU do.
