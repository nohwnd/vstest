using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestProject2
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
#if NET5_0
        public void TestMethod3_NET50()
#elif NETCOREAPP3_1
        public void TestMethod3_NETCOREAPP31()
#elif NET48
        public void TestMethod3_NET48()
#elif NET472
        public void TestMethod3_NET472()
#endif
        {
        }
    }
}
