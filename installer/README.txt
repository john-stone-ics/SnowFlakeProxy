SnowFlakeProxy
==============

Chooser names and ProgIDs:

  Wanderer Snowflake Filter Wheel 1 (Proxy)
    ASCOM.SnowFlakeProxy1.FilterWheel
    wraps ASCOM.WandererSnowflakeFilterWheel1.FilterWheel

  Wanderer Snowflake Filter Wheel 2 (Proxy)
    ASCOM.SnowFlakeProxy2.FilterWheel
    wraps ASCOM.WandererSnowflakeFilterWheel2.FilterWheel

  Wanderer Snowflake Filter Wheel 3 (Proxy)
    ASCOM.SnowFlakeProxy3.FilterWheel
    wraps ASCOM.WandererSnowflakeFilterWheel3.FilterWheel

Pick the proxy whose number matches the Wanderer slot for that wheel.

The unnumbered ProgID ASCOM.SnowFlakeProxy.FilterWheel is removed
on register; settings move to ASCOM.SnowFlakeProxy1.FilterWheel.
Re-select slot 1 in NINA/SkyGuard if it still points at the old id.

Do not connect anything directly to the matching vendor ProgID while
the proxy is in use.

Do not put JustAHub in the path.

Do not use a name-patched Wanderer executable. This proxy strips
"Filter N (name)" itself.

Prerequisites
-------------
- ASCOM Platform 7 or later
- .NET Framework 4.7.2 or later
- Unpatched Wanderer Snowflake Filter Wheel driver

Uninstall from Windows Settings > Apps, or rerun this setup and choose Remove.
