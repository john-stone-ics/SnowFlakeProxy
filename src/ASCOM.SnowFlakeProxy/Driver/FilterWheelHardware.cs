using System;
using System.Globalization;
using ASCOM.Utilities;
using ASCOM;

namespace ASCOM.SnowFlakeProxy
{
    [HardwareClass]
    internal static class FilterWheelHardware
    {
        private static readonly object init_lock = new object();
        private static readonly SnowflakeProxyController[] controllers = new SnowflakeProxyController[4];
        private static readonly ProxyLogger[] loggers = new ProxyLogger[4];

        internal static SnowflakeProxyController ControllerFor(int slot)
        {
            ProxySlotIdentity identity = ProxyIdentity.ForSlot(slot);
            lock (init_lock)
            {
                if (controllers[slot] == null)
                {
                    ProxySettings settings = ProxySettingsStore.Load(identity.proxy_prog_id);
                    TraceLogger trace_logger = new TraceLogger("", "SnowFlakeProxy.Hardware." + slot.ToString(CultureInfo.InvariantCulture));
                    trace_logger.Enabled = settings.trace_enabled;
                    ProxyLogger logger = new ProxyLogger(trace_logger);
                    ProxySlotIdentity captured = identity;
                    controllers[slot] = new SnowflakeProxyController(
                        delegate
                        {
                            return new WandererFilterWheelAdapter(captured.vendor_prog_id);
                        },
                        settings,
                        logger,
                        captured);
                    loggers[slot] = logger;
                }

                return controllers[slot];
            }
        }

        public static void Dispose()
        {
            lock (init_lock)
            {
                for (int slot = 1; slot <= 3; slot++)
                {
                    if (controllers[slot] != null)
                    {
                        controllers[slot].Dispose();
                        controllers[slot] = null;
                    }

                    loggers[slot] = null;
                }
            }
        }
    }
}
