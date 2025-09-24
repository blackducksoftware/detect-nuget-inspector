using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DetectNugetInspectorTests.ShantysTests
{
    [TestClass]
    public class KickOffTests
    {
        [TestMethod]
        public void TestBasicSetup()
        {
            var runner = new NITestRunner();
            var result = runner.RunBasicSetupTest("6.0.100", "MyTestSolution");
            
            Assert.IsTrue(result.Success, result.Message);
            Console.WriteLine(result.Message);
        }

        [TestMethod]
        public void TestBasicSetup_WithDifferentVersion()
        {
            var runner = new NITestRunner();
            var result = runner.RunBasicSetupTest("6.0.136", "AnotherTestSolution");
            
            Assert.IsTrue(result.Success, result.Message);
            Console.WriteLine(result.Message);
        }

        [TestMethod]
        public void TestBasicSetup_InvalidDotNetVersion()
        {
            var runner = new NITestRunner();
            
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                runner.RunBasicSetupTest("99.0.999", "FailureSolution");
            });
        }
    }
}