using Blackduck.Detect.Nuget.Inspector.Inspection;
using Microsoft.Build.Locator;

namespace DetectNugetInspectorTests.ShantysTests;

// Tests cases for scenarios where MSBuild is registered via MSBuildLocator need to be in their own class to avoid
// conflicts with other tests (MSBuild registration is process-wide and can only be done once per process.)
[TestClass]
public class TestsWithMsBuildRegistrationDotnet6
{
          [TestMethod]
        public void TestBasicSolution_DotNet6_DuplicatePackageReference_ProjectReferenceResolver()
        {
            // 1. Set up environment with .NET 6 (nuget v6.3.4.2)
            var dotnetVersion = "6.0.201";
            var env = new TestEnvironmentManager().SetupEnvironment(dotnetVersion, "dotnet6");

            // 2. Create .NET 6 solution
            var builder = new TestSolutionBuilder(env)
                .CreateSolution("MySimpleDotnet6Solution")
                .CreateAndAddProject("ProjectA")
                .AddDependencyToProject("ProjectA", "Newtonsoft.Json", "13.0.3")
                .AddPackageReferenceToCsprojManually("ProjectA", "Newtonsoft.Json", "12.0.1")
                .NoBuildArtifacts() // So we can force using ProjectReferenceResolver instead of assets file
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
                // Register MSBuild so ProjectReferenceResolver succeeds and does not cascade to XML resolver
                var instance = MSBuildLocator.RegisterDefaults();
                Console.WriteLine($"MSBuild registered: {instance.Name} {instance.Version} at {instance.MSBuildPath}");
                
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
                Assert.IsTrue(output.Contains("Reference resolver succeeded."));
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