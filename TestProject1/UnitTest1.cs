using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace TestProject1
{
    [TestClass]
    public class UnitTest1
    {
        // on net48 the first test is slow and the second is not
        // on other fmw the first is fast and the second is slow
        // we should see the results intersparsed in the output if
        // we run in parallel

#if NET48
        [TestMethod]
        public void TestMethod1_NET48_slow() {
            System.Threading.Thread.Sleep(3000);
        }

        [TestMethod]
        public void TestMethod2_NET48_fast() {
            // this one is fast
        }

        [TestMethod]
        public void TestMethod3_NET48_slow() {
            System.Threading.Thread.Sleep(3000);
        }
#else
        [TestMethod]
        public void TestMethod1_NET472_fast()
        {
            // this one is fast
        }

        [TestMethod]
        public void TestMethod2_NET472_slow()
        {
            // this one is slow, but net472 should still complete 
            // way before net48
            System.Threading.Thread.Sleep(3000);
        }
#endif
    }
}
