# SnowFlakeProxy architecture

```text
                         clients
             +-------------+-------------+
             |             |             |
            NINA        SkyGuard       other
             |             |             |
             +-------------+-------------+
                           |
                           v
            SnowFlakeProxy slot 1, 2, or 3
                 public cached state
                           |
                           v
             per-slot controller + STA worker
                           |
                           v
        matching Wanderer FilterWheel N LocalServer
                           |
                           v
                       hardware
```

**PUBLIC Position DOES NOT TOUCH HARDWARE.**

The public `Position` getter is a lock plus a memory read. It never calls `ASCOM.WandererSnowflakeFilterWheel1.FilterWheel`, never waits on the hardware worker, and never sleeps.

While a move is in progress the getter returns `-1`. Stale vendor slots such as the previous filter number are never copied into `cached_position`.

There is exactly one vendor COM object **per proxy slot**. It is created, used, and disposed only on that slot's STA thread (`SnowFlakeProxy Hardware Worker 1` / `2` / `3`). Maximum concurrent vendor calls on each worker is 1. The three slots are independent: connecting proxy 2 does not share state with proxy 1.

## Identity

| Slot | Proxy ProgID | Proxy Name | Vendor ProgID |
|---|---|---|---|
| 1 | `ASCOM.SnowFlakeProxy1.FilterWheel` | `Wanderer Snowflake Filter Wheel 1 (Proxy)` | `ASCOM.WandererSnowflakeFilterWheel1.FilterWheel` |
| 2 | `ASCOM.SnowFlakeProxy2.FilterWheel` | `Wanderer Snowflake Filter Wheel 2 (Proxy)` | `ASCOM.WandererSnowflakeFilterWheel2.FilterWheel` |
| 3 | `ASCOM.SnowFlakeProxy3.FilterWheel` | `Wanderer Snowflake Filter Wheel 3 (Proxy)` | `ASCOM.WandererSnowflakeFilterWheel3.FilterWheel` |

## Filter names

The unpatched vendor `Names` array is:

```text
Filter 1 (L)
Filter 2 (R)
Filter 3 (G)
Filter 4 (B)
Filter 5 (H)
Filter 6 (S)
Filter 7 (O)
Filter 8 (D)
```

The proxy exposes `L R G B H S O D` by stripping `^Filter\s+([1-9][0-9]*)\s+\((.*)\)$`. Unmatched names are unchanged.

Do not use a name-patched Wanderer binary. The proxy is the name fix.

## Operating rule

While the proxy owns the wheel, nothing else may connect to the vendor ProgID.
