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
                runner.RunBasicSetupTest("99.0.999", "FailureSolution", "nonExistentDotnetVersion");
            });
        }

        
        // dotnet 6 (nuget v6.3.4.2)
        [TestMethod]
        public void TestBasicSetup_DotNet6()
        {
            var runner = new NITestRunner();
            var result = runner.RunBasicSetupTest("6.0.201", "MyTestSolution", "dotnet6");

            Assert.IsTrue(result.Success, result.Message);
        }

        // dotnet 7 (nuget v6.7.1.1)
        [TestMethod]
        public void TestBasicSetup_DotNet7()
        {
            var runner = new NITestRunner();
            var result = runner.RunBasicSetupTest("7.0.410", "MyTestSolution", "dotnet7");

            Assert.IsTrue(result.Success, result.Message);
        }
        
        // dotnet 8 tests (nuget v...)
        [TestMethod]
        public void TestBasicSetup_DotNet8()
        {
            var runner = new NITestRunner();
            
            Assert.ThrowsException<InvalidOperationException>(() =>
            {
                runner.RunBasicSetupTest("8.0.0", "MyTestSolution", "dotnet8");
            });
        }

        [TestMethod]
        public void TestBasicSetup_DotNet6_NoCPM()
        {
            // 1. Set up environment with .NET 6
            var dotnetVersion = "6.0.428"; // todo change me to match what is on jenkins 
            var env = new TestEnvironmentManager().SetupEnvironment(dotnetVersion, "dotnet6");

            // 2. Build a .NET 6 project without CPM
            var builder = new TestSolutionBuilder(env).CreateSimpleSolution("MySimpleDotnet6Solution").Build();
          

            // 3. Run inspector and assert output

            // Make assertions on the result json
        }
    }
}