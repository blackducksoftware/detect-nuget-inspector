using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DetectNugetInspectorTests.ShantysTests
{
    [TestClass]
    public class KickOffTests
    {

        [TestMethod]
        public void TestBasicSetup_InvalidDotNetVersion()
        {
            var runner = new NITestRunner();

            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                runner.RunBasicSetupTest("99.0.999", "FailureSolution");
            });
        }

        [TestMethod]
        public void TestBasicSetup_DotNet6()
        {
            var runner = new NITestRunner();
            var result = runner.RunBasicSetupTest("6.0.428", "MyTestSolution", "dotnet6");

            Assert.IsTrue(result.Success, result.Message);
        }

        [TestMethod]
        public void TestBasicSetup_DotNet7()
        {
            var runner = new NITestRunner();
            var result = runner.RunBasicSetupTest("7.0.410", "MyTestSolution", "dotnet7");

            Assert.IsTrue(result.Success, result.Message);
        }
        
        [TestMethod]
        public void TestBasicSetup_DotNet8()
        {
            var runner = new NITestRunner();
            
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                runner.RunBasicSetupTest("8.0.0", "MyTestSolution", "dotnet8");
            });
        }
    }
}