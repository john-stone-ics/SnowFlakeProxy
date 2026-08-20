using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ASCOM;
using ASCOM.SnowFlakeProxy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SnowFlakeProxy.Tests
{
    [TestClass]
    public class PositionTests
    {
        [TestMethod]
        public void Position_WhenIdle_ReturnsCachedPosition()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(2);
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                Assert.AreEqual((short)2, controller.GetPosition(client_id));
            }
        }

        [TestMethod]
        public void Position_WhenStarting_ReturnsMinusOne()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            fake.position_set_delay_ms = 300;
            fake.position_get_delay_ms = 300;
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                Task setter = Task.Run(delegate
                {
                    controller.SetPosition(client_id, 1);
                });
                DateTime deadline = DateTime.UtcNow.AddSeconds(2);
                short seen = 0;
                while (DateTime.UtcNow < deadline)
                {
                    seen = controller.GetPosition(client_id);
                    if (seen == -1)
                    {
                        break;
                    }

                    Thread.Sleep(5);
                }

                Assert.AreEqual((short)-1, seen);
                setter.Wait(5000);
            }
        }

        [TestMethod]
        public void Position_WhenMoving_ReturnsMinusOne()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            fake.position_get_delay_ms = 400;
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                Task setter = Task.Run(delegate
                {
                    controller.SetPosition(client_id, 1);
                });
                Thread.Sleep(50);
                Assert.AreEqual((short)-1, controller.GetPosition(client_id));
                setter.Wait(5000);
                Assert.AreEqual((short)1, ControllerHarness.WaitForStationary(controller, client_id, 5000));
            }
        }

        [TestMethod]
        public void Position_WhenMoveCompletes_ReturnsTarget()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                controller.SetPosition(client_id, 3);
                Assert.AreEqual((short)3, ControllerHarness.WaitForStationary(controller, client_id, 5000));
            }
        }

        [TestMethod]
        public void Position_DoesNotCallUnderlyingGetter()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                int after_connect = fake.PositionGetCallCount;
                for (int index = 0; index < 25; index++)
                {
                    Assert.AreEqual((short)0, controller.GetPosition(client_id));
                }

                Assert.AreEqual(after_connect, fake.PositionGetCallCount);
            }
        }

        [TestMethod]
        public void Position_DoesNotBlockOnUnderlyingMove()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            fake.position_set_delay_ms = 50;
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                fake.position_get_delay_ms = 800;
                Task setter = Task.Run(delegate
                {
                    controller.SetPosition(client_id, 1);
                });
                Thread.Sleep(80);
                Stopwatch timer = Stopwatch.StartNew();
                short position = controller.GetPosition(client_id);
                timer.Stop();
                Assert.AreEqual((short)-1, position);
                Assert.IsTrue(timer.ElapsedMilliseconds < 100, "Public getter took " + timer.ElapsedMilliseconds + " ms");
                setter.Wait(5000);
            }
        }

        [TestMethod]
        public void Position_InvalidTarget_ThrowsInvalidValueException()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                try
                {
                    controller.SetPosition(client_id, 99);
                    Assert.Fail("Expected InvalidValueException.");
                }
                catch (InvalidValueException)
                {
                }
            }
        }

        [TestMethod]
        public void Position_WhenDisconnected_ThrowsNotConnectedException()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                try
                {
                    controller.GetPosition(Guid.NewGuid());
                    Assert.Fail("Expected NotConnectedException.");
                }
                catch (NotConnectedException)
                {
                }
            }
        }

        [TestMethod]
        public void Position_SameTargetDuringMove_DoesNotIssueSecondMove()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            fake.position_get_delay_ms = 200;
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                Task setter = Task.Run(delegate
                {
                    controller.SetPosition(client_id, 4);
                });
                Thread.Sleep(30);
                controller.SetPosition(client_id, 4);
                setter.Wait(5000);
                Assert.AreEqual((short)4, ControllerHarness.WaitForStationary(controller, client_id, 5000));
                Assert.AreEqual(1, fake.PositionSetCallCount);
            }
        }

        [TestMethod]
        public void Position_DifferentTargetDuringMove_ThrowsDriverException()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            fake.position_get_delay_ms = 250;
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                Task setter = Task.Run(delegate
                {
                    controller.SetPosition(client_id, 4);
                });
                Thread.Sleep(40);
                try
                {
                    controller.SetPosition(client_id, 1);
                    Assert.Fail("Expected DriverException.");
                }
                catch (DriverException)
                {
                }

                setter.Wait(5000);
                Assert.AreEqual(1, fake.PositionSetCallCount);
            }
        }

        [TestMethod]
        public void Position_StaleVendorPositionNeverLeaksToClient()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            fake.stale_reads_after_set = 3;
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                Task setter = Task.Run(delegate
                {
                    controller.SetPosition(client_id, 1);
                });
                DateTime started = DateTime.UtcNow.AddSeconds(2);
                while (DateTime.UtcNow < started && controller.GetPosition(client_id) != -1)
                {
                    Thread.Sleep(5);
                }

                DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < deadline)
                {
                    short seen = controller.GetPosition(client_id);
                    Assert.IsTrue(seen == -1 || seen == 1, "Client saw stale position " + seen);
                    if (seen == 1)
                    {
                        break;
                    }

                    Thread.Sleep(5);
                }

                setter.Wait(5000);
                Assert.AreEqual((short)1, controller.GetPosition(client_id));
            }
        }
    }
}
