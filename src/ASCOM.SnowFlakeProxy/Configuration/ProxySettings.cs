namespace ASCOM.SnowFlakeProxy
{
    internal sealed class ProxySettings
    {
        internal bool trace_enabled = true;
        internal bool normalize_filter_names = true;
        internal int move_timeout_ms = 60000;
        internal int position_retry_delay_ms = 250;
        internal int connect_timeout_ms = 60000;
        internal int setter_accept_timeout_ms = 30000;
    }
}
