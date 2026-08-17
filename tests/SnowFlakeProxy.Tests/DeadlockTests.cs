using System;
using System.Threading;
using System.Threading.Tasks;
using ASCOM.SnowFlakeProxy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SnowFlakeProxy.Tests
{
    [TestClass]
    public class DeadlockTests
    {
        [TestMethod]
        public void ConnectMoveDisconnect_WithReaders_DoesNotDeadlock()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                fake.position_get_delay_ms = 50;
                bool stop = false;
                Task reader = Task.Run(delegate
                {
                    while (!stop)
                    {
                        try
                        {
                            controller.GetPosition(client_id);
                        }
                        catch (Exception)
                        {
                        }

                        Thread.Sleep(5);
                    }
                });

                controller.SetPosition(client_id, 2);
                ControllerHarness.WaitForStationary(controller, client_id, 5000);
                controller.DisconnectBlocking(client_id);
                stop = true;
                Assert.IsTrue(reader.Wait(2000), "Reader task deadlocked.");
            }
        }
    }
}
