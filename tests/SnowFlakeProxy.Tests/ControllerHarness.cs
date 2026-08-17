using System;
using ASCOM.SnowFlakeProxy;

namespace SnowFlakeProxy.Tests
{
    internal static class ControllerHarness
    {
        internal static SnowflakeProxyController Create(FakeUnderlyingFilterWheel fake, ProxySettings settings)
        {
            if (settings == null)
            {
                settings = DefaultSettings();
            }

            return new SnowflakeProxyController(
                delegate
                {
                    return fake;
                },
                settings,
                ProxyLogger.CreateNull());
        }

        internal static ProxySettings DefaultSettings()
        {
            ProxySettings settings = new ProxySettings();
            settings.trace_enabled = false;
            settings.normalize_filter_names = true;
            settings.move_timeout_ms = 5000;
            settings.position_retry_delay_ms = 10;
            settings.connect_timeout_ms = 5000;
            settings.setter_accept_timeout_ms = 5000;
            return settings;
        }

        internal static Guid ConnectClient(SnowflakeProxyController controller)
        {
            Guid client_id = Guid.NewGuid();
            controller.ConnectBlocking(client_id);
            return client_id;
        }

        internal static short WaitForStationary(SnowflakeProxyController controller, Guid client_id, int timeout_ms)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeout_ms);
            short position = controller.GetPosition(client_id);
            while (position == -1)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("Timed out waiting for a stationary proxy position.");
                }

                System.Threading.Thread.Sleep(10);
                position = controller.GetPosition(client_id);
            }

            return position;
        }
    }
}
