# Baseline measurements

Recorded so future maintainers have a reason for every unusual piece of proxy logic.

## Vendor identity (2026-08-17, this machine)

Queried with `tools\Query-Wanderer.ps1` against the unmodified Wanderer Empire ASCOM driver.

```text
ProgID:            ASCOM.WandererSnowflakeFilterWheel1.FilterWheel
Name:              Wanderer Snowflake Filter Wheel 1
Description:       Wanderer Snowflake Filter Wheel 1   (null until Connected)
InterfaceVersion:  3
DriverVersion:     1.0
DriverInfo:        Firmware:20260124 ID:0
ASCOM Platform:    7.1
```

Proxy public `Name` must therefore be:

```text
Wanderer Snowflake Filter Wheel 1 (Proxy)
```

## Connected-session snapshot (wheel attached)

```text
Connected = true   duration_ms=12
Names (unpatched vendor driver; this is the live hardware configuration):
  [0] Filter 1 (L)
  [1] Filter 2 (R)
  [2] Filter 3 (G)
  [3] Filter 4 (B)
  [4] Filter 5 (H)
  [5] Filter 6 (S)
  [6] Filter 7 (O)
  [7] Filter 8 (D)
FocusOffsets: 0,0,0,0,0,0,0,0
Position: 1
Position GET duration_ms: 914
SupportedActions: empty
Connecting: not available (null)
Connected = false  duration_ms=2
```

The 2026-08-17 `Query-Wanderer.ps1` run that returned short names `L R G B H S O D` was against a name-patched Wanderer binary. That output is **not** the vendor baseline. Production proxy development and tests must assume the unpatched decorated names above.

Notes:

- The proxy must strip the Wanderer decoration (`Filter 1 (L)` → `L`) at connection time. Unmatched names pass through unchanged.
- A 914 ms stationary `Position` GET is already far above the proxy's 100 ms public-getter budget. Earlier lab measurements (below) showed multi-second blocks and stale slots under multi-client load. The proxy must never call the vendor `Position` getter on a public code path.

## Historical defect measurements (original specification)

These were measured against the untouched vendor driver before this proxy existed.

```text
Direct vendor:
0 -> 1
Position GET blocked 8445ms
returned target

Single client through hub:
Position GET blocked approximately 7020ms

Two-client case:
first getters blocked approximately 9000ms
returned stale old slot
new slot became visible approximately 18s after command

vendor Names (historical decorated form):
Filter 1 (L)
Filter 2 (R)
Filter 3 (G)
Filter 4 (B)
Filter 5 (H)
Filter 6 (S)
Filter 7 (O)
Filter 8 (D)

normalized Names:
L
R
G
B
H
S
O
D
```

## Operating rule

While SnowFlakeProxy owns the wheel, no other application may connect to `ASCOM.WandererSnowflakeFilterWheel1.FilterWheel`. Direct vendor access bypasses serialization and invalidates cached proxy state.
