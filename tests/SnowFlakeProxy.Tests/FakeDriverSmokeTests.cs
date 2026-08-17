using Microsoft.VisualStudio.TestTools.UnitTesting;
using ASCOM.SnowFlakeProxy;

namespace SnowFlakeProxy.Tests
{
    [TestClass]
    public class FakeDriverSmokeTests
    {
        [TestMethod]
        public void Fake_TracksConcurrentCallsAndLatency()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            fake.position_get_delay_ms = 50;
            fake.SetStationaryPosition(0);
            fake.Connected = true;
            short first = fake.Position;
            fake.Position = 1;
            short second = fake.Position;
            fake.Connected = false;

            Assert.AreEqual((short)0, first);
            Assert.AreEqual((short)1, second);
            Assert.AreEqual(1, fake.ConnectCallCount);
            Assert.AreEqual(1, fake.DisconnectCallCount);
            Assert.AreEqual(1, fake.PositionSetCallCount);
            Assert.AreEqual(2, fake.PositionGetCallCount);
            Assert.AreEqual(1, fake.MaximumConcurrentVendorCallCount);
            Assert.AreEqual(0, fake.ConcurrentVendorCallCount);
        }
    }
}
