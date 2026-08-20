using Microsoft.VisualStudio.TestTools.UnitTesting;
using ASCOM.SnowFlakeProxy;

namespace SnowFlakeProxy.Tests
{
    [TestClass]
    public class IdentityTests
    {
        [TestMethod]
        public void SlotProgIdsMatchWandererNumbering()
        {
            Assert.AreEqual("ASCOM.SnowFlakeProxy1.FilterWheel", ProxyIdentity.Slot1.proxy_prog_id);
            Assert.AreEqual("ASCOM.SnowFlakeProxy2.FilterWheel", ProxyIdentity.Slot2.proxy_prog_id);
            Assert.AreEqual("ASCOM.SnowFlakeProxy3.FilterWheel", ProxyIdentity.Slot3.proxy_prog_id);
            Assert.AreEqual("ASCOM.WandererSnowflakeFilterWheel1.FilterWheel", ProxyIdentity.Slot1.vendor_prog_id);
            Assert.AreEqual("Wanderer Snowflake Filter Wheel 1 (Proxy)", ProxyIdentity.Slot1.proxy_name);
            Assert.AreEqual("ASCOM.SnowFlakeProxy.FilterWheel", ProxyIdentity.unnumbered_legacy_prog_id);
        }
    }
}
