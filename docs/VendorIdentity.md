# Vendor identity constants

Do not guess these values. They were read from the live Wanderer COM driver on 2026-08-17.

Wanderer registers three Chooser slots so one PC can host up to three Snowflakes. Each proxy slot wraps the matching vendor ProgID.

```text
vendor_driver_name_1 = Wanderer Snowflake Filter Wheel 1
vendor_driver_name_2 = Wanderer Snowflake Filter Wheel 2
vendor_driver_name_3 = Wanderer Snowflake Filter Wheel 3

proxy_name_1 = Wanderer Snowflake Filter Wheel 1 (Proxy)
proxy_name_2 = Wanderer Snowflake Filter Wheel 2 (Proxy)
proxy_name_3 = Wanderer Snowflake Filter Wheel 3 (Proxy)

vendor_prog_id_1 = ASCOM.WandererSnowflakeFilterWheel1.FilterWheel
vendor_prog_id_2 = ASCOM.WandererSnowflakeFilterWheel2.FilterWheel
vendor_prog_id_3 = ASCOM.WandererSnowflakeFilterWheel3.FilterWheel

proxy_prog_id_1 = ASCOM.SnowFlakeProxy1.FilterWheel
proxy_prog_id_2 = ASCOM.SnowFlakeProxy2.FilterWheel
proxy_prog_id_3 = ASCOM.SnowFlakeProxy3.FilterWheel
```
