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
                  SnowFlakeProxy
                 public cached state
                           |
                           v
                  shared controller
                           |
                           v
                single hardware worker
                           |
                           v
        Wanderer ASCOM FilterWheel LocalServer
                           |
                           v
                       hardware
```

**PUBLIC Position DOES NOT TOUCH HARDWARE.**

The public `Position` getter is a lock plus a memory read. It never calls `ASCOM.WandererSnowflakeFilterWheel1.FilterWheel`, never waits on the hardware worker, and never sleeps.

While a move is in progress the getter returns `-1`. Stale vendor slots such as the previous filter number are never copied into `cached_position`.

There is exactly one vendor COM object. It is created, used, and disposed only on the STA thread named `SnowFlakeProxy Hardware Worker`. Maximum concurrent vendor calls is 1.

## Identity

- Proxy ProgID: `ASCOM.SnowFlakeProxy.FilterWheel`
- Proxy Name: `Wanderer Snowflake Filter Wheel 1 Proxy`
- Vendor ProgID: `ASCOM.WandererSnowflakeFilterWheel1.FilterWheel`
- Vendor Name: `Wanderer Snowflake Filter Wheel 1`

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
