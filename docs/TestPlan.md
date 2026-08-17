# Test plan

## Automated (no hardware)

```text
msbuild SnowFlakeProxy.sln /p:Configuration=Debug /p:Platform="Any CPU"
vstest.console.exe tests\SnowFlakeProxy.Tests\bin\Debug\net472\SnowFlakeProxy.Tests.dll /Platform:x86
```

Covers connection leases, name normalization, Position state machine, stale-slot suppression, concurrency, latency, fault recovery, and deadlock.

## Hardware (wheel attached, elevated registration)

1. Build Release.
2. Elevated: `tools\Register-Driver.ps1`
3. `tools\Query-Wanderer.ps1` against the **unpatched** vendor driver if needed for comparison.
4. `tools\Test-ProxySingleClient.ps1` — expect `Position = -1` then target; getter under 100 ms.
5. `tools\Test-ProxyMultiClient.ps1` — both processes see `-1` then target; one physical move.
6. ConformU full FilterWheel run against `ASCOM.SnowFlakeProxy.FilterWheel`. Save report under `docs\ConformU\`.
7. NINA only, SkyGuard only, then both. Choose **Wanderer Snowflake Filter Wheel 1 (Proxy)**, not the vendor driver and not JustAHub.

## NINA / SkyGuard checklist

- Filter list: L R G B H S O D
- Moves: L→R→G→B→H→S→O→D→L
- No NINA “Failed to move filter wheel”
- Both apps show `-1` during a move commanded by NINA, then the same final filter
- One client disconnect does not disconnect the other
