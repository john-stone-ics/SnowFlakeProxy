using System;
using ASCOM.Utilities;
using ASCOM;

namespace ASCOM.SnowFlakeProxy
{
    [HardwareClass]
    internal static class FilterWheelHardware
    {
        private static readonly object init_lock = new object();
        private static SnowflakeProxyController controller;
        private static ProxyLogger logger;

        internal static SnowflakeProxyController Controller
        {
            get
            {
                lock (init_lock)
                {
                    if (controller == null)
                    {
                        ProxySettings settings = ProxySettingsStore.Load();
                        TraceLogger trace_logger = new TraceLogger("", "SnowFlakeProxy.Hardware");
                        trace_logger.Enabled = settings.trace_enabled;
                        logger = new ProxyLogger(trace_logger);
                        controller = new SnowflakeProxyController(
                            delegate
                            {
                                return new WandererFilterWheelAdapter();
                            },
                            settings,
                            logger);
                    }

                    return controller;
                }
            }
        }

        public static void Dispose()
        {
            lock (init_lock)
            {
                if (controller != null)
                {
                    controller.Dispose();
                    controller = null;
                }

                logger = null;
            }
        }
    }
}
