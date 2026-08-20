using System;

namespace ASCOM.SnowFlakeProxy
{
    internal sealed class ProxySlotIdentity
    {
        internal readonly int slot;
        internal readonly string vendor_prog_id;
        internal readonly string proxy_prog_id;
        internal readonly string proxy_name;

        internal ProxySlotIdentity(int slot, string vendor_prog_id, string proxy_prog_id, string proxy_name)
        {
            this.slot = slot;
            this.vendor_prog_id = vendor_prog_id;
            this.proxy_prog_id = proxy_prog_id;
            this.proxy_name = proxy_name;
        }
    }

    internal static class ProxyIdentity
    {
        internal const string Description = "Multi-client ASCOM proxy for Wanderer Snowflake filter wheel";
        internal const string DriverVersion = "0.1";
        internal const short InterfaceVersion = 3;

        internal const string proxy_prog_id_1 = "ASCOM.SnowFlakeProxy1.FilterWheel";
        internal const string proxy_prog_id_2 = "ASCOM.SnowFlakeProxy2.FilterWheel";
        internal const string proxy_prog_id_3 = "ASCOM.SnowFlakeProxy3.FilterWheel";
        internal const string unnumbered_legacy_prog_id = "ASCOM.SnowFlakeProxy.FilterWheel";

        internal const string chooser_name_1 = "Wanderer Snowflake Filter Wheel 1 (Proxy)";
        internal const string chooser_name_2 = "Wanderer Snowflake Filter Wheel 2 (Proxy)";
        internal const string chooser_name_3 = "Wanderer Snowflake Filter Wheel 3 (Proxy)";

        internal static readonly ProxySlotIdentity Slot1 = new ProxySlotIdentity(
            1,
            "ASCOM.WandererSnowflakeFilterWheel1.FilterWheel",
            proxy_prog_id_1,
            chooser_name_1);

        internal static readonly ProxySlotIdentity Slot2 = new ProxySlotIdentity(
            2,
            "ASCOM.WandererSnowflakeFilterWheel2.FilterWheel",
            proxy_prog_id_2,
            chooser_name_2);

        internal static readonly ProxySlotIdentity Slot3 = new ProxySlotIdentity(
            3,
            "ASCOM.WandererSnowflakeFilterWheel3.FilterWheel",
            proxy_prog_id_3,
            chooser_name_3);

        internal static ProxySlotIdentity ForSlot(int slot)
        {
            switch (slot)
            {
                case 1:
                    return Slot1;
                case 2:
                    return Slot2;
                case 3:
                    return Slot3;
                default:
                    throw new ArgumentOutOfRangeException("slot");
            }
        }
    }
}
