using Blackduck.Detect.Nuget.Inspector.Inspection;
using Microsoft.Build.Locator;

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
        

        // dotnet 7.0.410 (nuget v6.7.1.1)
        /*[TestMethod]
        public void TestBasicSetup_DotNet7()
        {
            var runner = new NITestRunner();
            var result = runner.RunBasicSetupTest("7.0.410", "MyTestSolution", "dotnet7");

            Assert.IsTrue(result.Success, result.Message);
        }*/

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
        
        [TestMethod]  
        public void TestBasicSolution_DotNet6_DuplicatePackageReference_XMLResolver()
        {
            // 1. Set up environment with .NET 6 (nuget v6.3.4.2)
            var dotnetVersion = "6.0.428";
            var env = new TestEnvironmentManager().SetupEnvironment(dotnetVersion, "dotnet6");

            // 2. Create .NET 6 solution
            var builder = new TestSolutionBuilder(env)
                .CreateSolution("MySimpleDotnet6Solution")
                .CreateAndAddProject("ProjectA")
                .AddDependencyToProject("ProjectA", "Newtonsoft.Json", "13.0.3")
                .AddPackageReferenceToCsprojManually("ProjectA", "Newtonsoft.Json", "12.0.1")
                .NoBuildArtifacts() // So we can bypass assets file during cascading
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
                // Since we do not register MSBuild, we will cascade to ProjectXMLResolver
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
                var dependencies = projectContainer.Dependencies;
                Assert.AreEqual(2, dependencies.Count);
                // Confirm duplicates are captured
                Assert.IsTrue(dependencies.Any(d => d.Name == "Newtonsoft.Json" && d.Version == "13.0.3"));
                Assert.IsTrue(dependencies.Any(d => d.Name == "Newtonsoft.Json" && d.Version == "12.0.1"));

                // Assert console output
                string output = stringWriter.ToString();
                Assert.IsTrue(output.Contains("Using backup XML resolver."));
                originalOut.Write(stringWriter.ToString());
            }
            finally
            {
                // Undo redirect, go back to writing to standard out
                Console.SetOut(originalOut);
                env.Cleanup();
            }
        }

        // for dotnet6, we could add .csproj branch, raw xml parser branch ... etc. 
        // So what tests is REQUIRED so you can close your tickets:
        // 1. for validating nuget up to 6.3.4: 
        // duplicate PackageReference in .csproj file. Confirm both are captured. 
        // Duplicate PackageVersion in Directory.Packages.props. Confirm both are captured.

        // 2. for validating nuget up to 6.7.1:
        // Central Package Management. Create solution with that very complicated set up. Confirm all captured.
    }
}