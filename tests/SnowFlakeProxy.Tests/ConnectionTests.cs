using System;
using ASCOM;
using ASCOM.SnowFlakeProxy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SnowFlakeProxy.Tests
{
    [TestClass]
    public class ConnectionTests
    {
        [TestMethod]
        public void FirstClientConnect_ConnectsVendorOnce()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                Assert.IsTrue(controller.IsClientConnected(client_id));
                Assert.AreEqual(1, fake.ConnectCallCount);
            }
        }

        [TestMethod]
        public void SecondClientConnect_DoesNotReconnectVendor()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid first = ControllerHarness.ConnectClient(controller);
                Guid second = ControllerHarness.ConnectClient(controller);
                Assert.IsTrue(controller.IsClientConnected(first));
                Assert.IsTrue(controller.IsClientConnected(second));
                Assert.AreEqual(1, fake.ConnectCallCount);
            }
        }

        [TestMethod]
        public void RepeatedConnectBySameClient_IsIdempotent()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = Guid.NewGuid();
                controller.ConnectBlocking(client_id);
                controller.ConnectBlocking(client_id);
                controller.ConnectBlocking(client_id);
                Assert.AreEqual(1, fake.ConnectCallCount);
                controller.DisconnectBlocking(client_id);
                Assert.AreEqual(1, fake.DisconnectCallCount);
            }
        }

        [TestMethod]
        public void OneClientDisconnect_LeavesVendorConnectedWhenAnotherLeaseExists()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid first = ControllerHarness.ConnectClient(controller);
                Guid second = ControllerHarness.ConnectClient(controller);
                controller.DisconnectBlocking(first);
                Assert.IsFalse(controller.IsClientConnected(first));
                Assert.IsTrue(controller.IsClientConnected(second));
                Assert.AreEqual(0, fake.DisconnectCallCount);
            }
        }

        [TestMethod]
        public void LastClientDisconnect_DisconnectsVendor()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                controller.DisconnectBlocking(client_id);
                Assert.IsFalse(controller.IsClientConnected(client_id));
                Assert.AreEqual(1, fake.DisconnectCallCount);
            }
        }

        [TestMethod]
        public void RepeatedDisconnect_IsIdempotent()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                controller.DisconnectBlocking(client_id);
                controller.DisconnectBlocking(client_id);
                Assert.AreEqual(1, fake.DisconnectCallCount);
            }
        }

        [TestMethod]
        public void DisposeOfOneClient_DoesNotDisconnectOtherClients()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid first = ControllerHarness.ConnectClient(controller);
                Guid second = ControllerHarness.ConnectClient(controller);
                controller.ReleaseLease(first);
                Assert.IsTrue(controller.IsClientConnected(second));
                Assert.AreEqual(0, fake.DisconnectCallCount);
            }
        }

        [TestMethod]
        public void SimultaneousFirstConnects_CoalesceToOneVendorConnection()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.connect_delay_ms = 200;
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid first = Guid.NewGuid();
                Guid second = Guid.NewGuid();
                System.Threading.Tasks.Task task_a = System.Threading.Tasks.Task.Run(delegate
                {
                    controller.ConnectBlocking(first);
                });
                System.Threading.Tasks.Task task_b = System.Threading.Tasks.Task.Run(delegate
                {
                    controller.ConnectBlocking(second);
                });
                System.Threading.Tasks.Task.WaitAll(task_a, task_b);
                Assert.IsTrue(controller.IsClientConnected(first));
                Assert.IsTrue(controller.IsClientConnected(second));
                Assert.AreEqual(1, fake.ConnectCallCount);
            }
        }

        [TestMethod]
        public void FailedPhysicalConnect_FailsAllWaitingLeases_AndAllowsRetry()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.throw_on_connect = true;
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = Guid.NewGuid();
                try
                {
                    controller.ConnectBlocking(client_id);
                    Assert.Fail("Expected connect failure.");
                }
                catch (DriverException)
                {
                }

                Assert.IsFalse(controller.IsClientConnected(client_id));
                fake.throw_on_connect = false;
                controller.ConnectBlocking(client_id);
                Assert.IsTrue(controller.IsClientConnected(client_id));
                Assert.AreEqual(2, fake.ConnectCallCount);
            }
        }

        [TestMethod]
        public void ClientWithoutLease_ThrowsNotConnected_WhileAnotherClientIsConnected()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid connected = ControllerHarness.ConnectClient(controller);
                Guid stranger = Guid.NewGuid();
                try
                {
                    controller.GetPosition(stranger);
                    Assert.Fail("Expected NotConnectedException.");
                }
                catch (NotConnectedException)
                {
                }

                Assert.IsTrue(controller.IsClientConnected(connected));
            }
        }
    }
}
