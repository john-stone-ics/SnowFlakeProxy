using ASCOM.SnowFlakeProxy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SnowFlakeProxy.Tests
{
    [TestClass]
    public class NameNormalizationTests
    {
        [TestMethod]
        public void DecoratedWandererNames_AreStripped()
        {
            Assert.AreEqual("L", FilterNameNormalizer.Normalize("Filter 1 (L)"));
            Assert.AreEqual("R", FilterNameNormalizer.Normalize("Filter 2 (R)"));
            Assert.AreEqual("D", FilterNameNormalizer.Normalize("Filter 8 (D)"));
            Assert.AreEqual("Ha 3nm", FilterNameNormalizer.Normalize("Filter 5 (Ha 3nm)"));
        }

        [TestMethod]
        public void UnmatchedNames_AreUnchanged()
        {
            Assert.AreEqual("Luminance", FilterNameNormalizer.Normalize("Luminance"));
            Assert.AreEqual("Ha (3nm)", FilterNameNormalizer.Normalize("Ha (3nm)"));
            Assert.AreEqual("Custom Filter", FilterNameNormalizer.Normalize("Custom Filter"));
        }

        [TestMethod]
        public void ControllerNames_AreNormalizedAndCopied()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                System.Guid client_id = ControllerHarness.ConnectClient(controller);
                string[] names_a = controller.GetNames(client_id);
                CollectionAssert.AreEqual(new string[] { "L", "R", "G", "B", "H", "S", "O", "D" }, names_a);
                names_a[0] = "CORRUPTED";
                string[] names_b = controller.GetNames(client_id);
                Assert.AreEqual("L", names_b[0]);
            }
        }

        [TestMethod]
        public void FocusOffsets_AreCopied()
        {
            FakeUnderlyingFilterWheel fake = new FakeUnderlyingFilterWheel();
            using (SnowflakeProxyController controller = ControllerHarness.Create(fake, null))
            {
                System.Guid client_id = ControllerHarness.ConnectClient(controller);
                int[] offsets_a = controller.GetFocusOffsets(client_id);
                offsets_a[0] = 999;
                int[] offsets_b = controller.GetFocusOffsets(client_id);
                Assert.AreEqual(0, offsets_b[0]);
            }
        }
    }
}
