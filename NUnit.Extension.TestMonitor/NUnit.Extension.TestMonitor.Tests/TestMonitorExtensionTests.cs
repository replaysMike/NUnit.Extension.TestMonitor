using NUnit.Framework;
using System.Threading.Tasks;

namespace NUnit.Extension.TestMonitor.Tests
{
    [TestFixture]
    public class TestMonitorExtensionTests
    {
        [Test]
        public async Task Should_Work()
        {
            TestContext.WriteLine($"Running {nameof(Should_Work)}");
            //await Task.Delay(1 * 1000);
            Assert.AreEqual(true, true);
        }
    }
}
