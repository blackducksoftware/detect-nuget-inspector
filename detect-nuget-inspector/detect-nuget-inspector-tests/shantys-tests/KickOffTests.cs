using System;
using Blackduck.Detect.Nuget.Inspector.Inspection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.ProjectModel;

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
        public void TestBasicSolution_DotNet6_ProjectAssetsJsonFile()
        {
            // 1. Set up environment with .NET 6 (nuget v6.3.4.2)
            var dotnetVersion = "6.0.428"; // todo change me to match what is on jenkins 
            var env = new TestEnvironmentManager().SetupEnvironment(dotnetVersion, "dotnet6");

            // 2. Create .NET 6 solution
            var builder = new TestSolutionBuilder(env)
                .CreateSolution("MySimpleDotnet6Solution")
                .CreateAndAddProject("ProjectA")
                .AddDependencyToProject("ProjectA", "Newtonsoft.Json", "13.0.3")
                .Build();
            
            // 3. Run inspector
            // Redirect console output for assertions later
            var stringWriter = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(stringWriter);
            
            var options = new InspectionOptions()
            {
                TargetPath = builder,
                Verbose = true,
                PackagesRepoUrl = "https://api.nuget.org/v3/index.json",
                OutputDirectory = env.WorkingDirectory,
                IgnoreFailure = false
            };

            try
            {
                var inspection = InspectorExecutor.ExecuteInspectors(options);

                // 4. Assert inspection results
                Assert.IsTrue(inspection.Success);
                var inspectionResults = inspection.Results;
                Assert.IsNotNull(inspectionResults);
                Assert.AreEqual(1, inspectionResults.Count);
                var result = inspectionResults[0]; 
                Assert.AreEqual(InspectionResult.ResultStatus.Success, result.Status);
                Assert.IsNotNull(result.Containers);
                Assert.AreEqual(1, result.Containers.Count);
                var solutionContainer = result.Containers[0];
                Assert.AreEqual(solutionContainer.Type, "Solution");
                Assert.AreEqual("MySimpleDotnet6Solution", solutionContainer.Name);
                
                var projectContainer = solutionContainer.Children[0];
                Assert.AreEqual(projectContainer.Type, "Project");
                Assert.AreEqual("ProjectA", projectContainer.Name);
                
                Assert.IsNotNull(projectContainer.Dependencies);
                Assert.AreEqual(1, projectContainer.Dependencies.Count);
                var dependencies = projectContainer.Dependencies;
                Assert.AreEqual(1, dependencies.Count);
                var dependency = dependencies.Single();
                Assert.AreEqual("Newtonsoft.Json", dependency.Name);
                Assert.AreEqual("13.0.3", dependency.Version);
                
                // Assert console output
                string output = stringWriter.ToString();
                Assert.IsTrue(output.Contains("Using assets json file:"));
                originalOut.Write(stringWriter.ToString());
            }
            finally
            {
                // Undo redirect, go back to writing to standard out
                Console.SetOut(originalOut);
                env.Cleanup();
            }
        }
    }
}