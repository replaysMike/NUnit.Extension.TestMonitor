using NUnit.Framework;
using System.Threading.Tasks;

namespace NUnit.Extension.TestMonitor.Tests
{
    [TestFixture]
    public class TestMonitorExtensionTests
    {
        [Test]
        public async Task Should_Test1_Work()
        {
            TestContext.WriteLine($"Running {nameof(Should_Test1_Work)}");
            await Task.Delay(3 * 1000);
            Assert.AreEqual(true, true);
        }

        [Test]
        public async Task Should_Test2_Work()
        {
            TestContext.WriteLine($"Running {nameof(Should_Test2_Work)}");
            await Task.Delay(5 * 1000);
            Assert.AreEqual(true, true);
        }

        [Test]
        public async Task Should_Test3_Work()
        {
            TestContext.WriteLine($"Running {nameof(Should_Test3_Work)}");
            await Task.Delay(7 * 1000);
            Assert.AreEqual(true, true);
        }

        [Test]
        public async Task Should_Test4_Work()
        {
            TestContext.WriteLine($"Running {nameof(Should_Test3_Work)}");
            await Task.Delay(3 * 1000);
            Assert.AreEqual(true, true);
            //Assert.AreEqual(true, false, "Example error message");
            // throw new System.Exception("This test failed, stack trace is as follows.");
        }
    }
}
