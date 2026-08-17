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
    public class ConcurrencyTests
    {
        [TestMethod]
        public void TwentyClients_SeeMinusOne_AndVendorConcurrencyIsOne()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                fake.position_get_delay_ms = 2000;
                Task setter = Task.Run(delegate
                {
                    controller.SetPosition(client_id, 1);
                });
                Thread.Sleep(60);

                Task[] readers = new Task[20];
                for (int index = 0; index < readers.Length; index++)
                {
                    readers[index] = Task.Run(delegate
                    {
                        for (int poll = 0; poll < 20; poll++)
                        {
                            Stopwatch timer = Stopwatch.StartNew();
                            short position = controller.GetPosition(client_id);
                            timer.Stop();
                            Assert.AreEqual((short)-1, position);
                            Assert.IsTrue(timer.ElapsedMilliseconds < 100, "Getter took " + timer.ElapsedMilliseconds + " ms");
                            Thread.Sleep(5);
                        }
                    });
                }

                Task.WaitAll(readers);
                setter.Wait(5000);
                Assert.AreEqual(1, fake.MaximumConcurrentVendorCallCount);
            }
        }

        [TestMethod]
        public void PublicGetterLatency_WhileVendorBlocked_StaysUnder100ms()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                fake.position_get_delay_ms = 1000;
                Task setter = Task.Run(delegate
                {
                    controller.SetPosition(client_id, 1);
                });
                Thread.Sleep(50);
                for (int index = 0; index < 100; index++)
                {
                    Stopwatch timer = Stopwatch.StartNew();
                    short position = controller.GetPosition(client_id);
                    timer.Stop();
                    Assert.AreEqual((short)-1, position);
                    Assert.IsTrue(timer.ElapsedMilliseconds < 100, "Getter " + index + " took " + timer.ElapsedMilliseconds + " ms");
                }

                setter.Wait(5000);
            }
        }
    }

    [TestClass]
    public class ErrorRecoveryTests
    {
        [TestMethod]
        public void Position_VendorSetterFailureDoesNotLeaveMovingForever()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            fake.throw_on_set = true;
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                try
                {
                    controller.SetPosition(client_id, 1);
                    Assert.Fail("Expected setter failure.");
                }
                catch (DriverException)
                {
                }

                try
                {
                    controller.GetPosition(client_id);
                    Assert.Fail("Expected faulted getter.");
                }
                catch (DriverException)
                {
                }
            }
        }

        [TestMethod]
        public void Position_MonitorFailureTransitionsToFault()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                fake.throw_on_get = true;
                try
                {
                    controller.SetPosition(client_id, 1);
                }
                catch (DriverException)
                {
                }

                DateTime deadline = DateTime.UtcNow.AddSeconds(2);
                bool faulted = false;
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        short position = controller.GetPosition(client_id);
                        if (position == -1)
                        {
                            Thread.Sleep(20);
                            continue;
                        }
                    }
                    catch (DriverException)
                    {
                        faulted = true;
                        break;
                    }
                }

                Assert.IsTrue(faulted, "Monitor failure did not transition to Faulted.");
            }
        }

        [TestMethod]
        public void Position_SetAfterMoveFault_RecoversAndStartsNewMove()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            fake.throw_on_set = true;
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                try
                {
                    controller.SetPosition(client_id, 1);
                }
                catch (DriverException)
                {
                }

                fake.throw_on_set = false;
                controller.SetPosition(client_id, 2);
                Assert.AreEqual((short)2, ControllerHarness.WaitForStationary(controller, client_id, 5000));
            }
        }

        [TestMethod]
        public void Position_LateVendorCompletionAfterTimeoutFault_RestoresIdle()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.SetStationaryPosition(0);
            ProxySettings settings = ControllerHarness.DefaultSettings();
            settings.move_timeout_ms = 50;
            settings.position_retry_delay_ms = 10;
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, settings))
            {
                Guid client_id = ControllerHarness.ConnectClient(controller);
                fake.position_get_delay_ms = 200;
                fake.stale_reads_after_set = 0;
                try
                {
                    controller.SetPosition(client_id, 1);
                }
                catch (DriverException)
                {
                }

                DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                short last = -2;
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        last = controller.GetPosition(client_id);
                        if (last == 1)
                        {
                            break;
                        }
                    }
                    catch (DriverException)
                    {
                    }

                    Thread.Sleep(20);
                }

                Assert.AreEqual((short)1, last);
            }
        }
    }
}
