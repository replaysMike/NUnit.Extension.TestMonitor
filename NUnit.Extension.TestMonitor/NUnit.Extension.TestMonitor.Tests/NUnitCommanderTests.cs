using NUnit.Framework;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NUnit.Extension.TestMonitor.Tests
{
    /// <summary>
    /// Tests are designed to test behavior for NUnit.Commander, an external component.
    /// </summary>
    [TestFixture]
    public class NUnitCommanderTests
    {
        [Test]
        public async Task Should_Test1_Work()
        {
            TestContext.WriteLine($"Running {nameof(Should_Test1_Work)}");
            await Task.Delay(1 * 1000);
            if (true)
                Assert.AreEqual(true, true);
            else
            {
                // enable this for random failures
                using (var rg = new RNGCryptoServiceProvider())
                {
                    byte[] rno = new byte[5];
                    rg.GetBytes(rno);
                    var val = BitConverter.ToInt32(rno, 0);
                    Assert.AreEqual(0, val % 2, "Random test failure");
                }
            }
        }

        [Test]
        public async Task Should_Test2_Work()
        {
            TestContext.WriteLine($"Running {nameof(Should_Test2_Work)}");
            await Task.Delay(1 * 1000);
            Assert.AreEqual(true, true);
        }

        [Test]
        public async Task Should_Test3_Work()
        {
            TestContext.WriteLine($"Running {nameof(Should_Test3_Work)}");
            await Task.Delay(1 * 1000);
            Assert.AreEqual(true, true);
        }

        [Test]
        public async Task Should_Test4_Work()
        {
            TestContext.WriteLine($"Running {nameof(Should_Test3_Work)}");
            await Task.Delay(1 * 1000);
            Assert.AreEqual(true, true);
            //Assert.AreEqual(true, false, "Fake error message");
            // throw new System.Exception("This test failed, stack trace is as follows.");
        }

        [Test]
        [Ignore("Test ignore message")]
        public async Task Should_Test5_BeIgnored()
        {
            TestContext.WriteLine($"Running {nameof(Should_Test3_Work)}");
            await Task.Delay(1 * 1000);
            Assert.AreEqual(true, false);
        }
    }
}
