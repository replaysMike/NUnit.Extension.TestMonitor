using NUnit.Framework;
using System;

namespace NUnit.Extension.TestMonitor.Tests
{
    [TestFixture]
    public class TestMonitorExtensionTests
    {
        [Test]
        public void Should_Work()
        {
            TestContext.WriteLine($"Running {nameof(Should_Work)}");
            Assert.AreEqual(true, true);
        }
    }
}
