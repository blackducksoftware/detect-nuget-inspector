using Blackduck.Detect.Nuget.Inspector.Inspection;

namespace detect_nuget_inspector_tests.versioned_dotnet_tests
{
    // To set up your environment for these tests, see set-up-versioned-dotnet-tests-env.sh
    [TestClass]
    public class KickOffTests
    {
        public TestContext TestContext { get; set; }
        private bool _testFailed;

        [TestInitialize]
        public void PrintTestName()
        {
            Console.WriteLine($"Starting test: {TestContext.TestName}");
        }

        [TestCleanup]
        public void PrintTestResult()
        {
            var outcome = TestContext.CurrentTestOutcome;
            Console.WriteLine(outcome == UnitTestOutcome.Passed
                ? $"✅ Test {TestContext.TestName} PASSED"
                : $"❌ Test {TestContext.TestName} FAILED");
        }


        [TestMethod]
        public void TestBasicSetup_InvalidDotNetVersion()
        {
            try
            {
                Assert.ThrowsException<InvalidOperationException>(() =>
                {
                    var env = new TestEnvironmentManager().SetupEnvironment("99.0.999", "nonExistentDotnetVersion");
                });
            }
            catch
            {
                _testFailed = true;
                throw;
            }
        }

        [TestMethod]
        public void TestBasicSolution_DotNet6_ProjectAssetsJsonFile()
        {
            // 1. Set up environment with .NET 6 (nuget v6.3.4.2)
            var dotnetVersion = "6.0.201";
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
            catch
            {
                _testFailed = true;
                throw;
            }
            finally
            {
                // Undo redirect, go back to writing to standard out
                Console.SetOut(originalOut);
                env.Cleanup();
            }
        }

        [TestMethod]
        public void TestSolution_DotNet6_DuplicatePackageReference_XMLResolver()
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
                .RemoveBuildArtifacts() // So we can bypass assets file during cascading
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
            catch
            {
                _testFailed = true;
                throw;
            }
            finally
            {
                // Undo redirect, go back to writing to standard out
                Console.SetOut(originalOut);
                env.Cleanup();
            }
        }

        /*[TestMethod]
        public void TestCPMSolution_DotNet7_ProjectAssetsJsonFile()
        {
            // 1. Set up environment with .NET 7 (nuget v6.7.1.1)
            var dotnetVersion = "7.0.410";
            var env = new TestEnvironmentManager().SetupEnvironment(dotnetVersion, "dotnet7");

            // 2. Create solution and projects with CPM enabled
            var builder = new TestSolutionBuilder(env)
                .CreateSolution("MyCPMDotnet7Solution")
                .EnableCentralPackageManagementWithDesiredStructure()
                .Build();

            // 3. Run inspector
            // Redirect console output for assertions later
            var stringWriter = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(stringWriter);
            
            var options = new InspectionOptions
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

                // 4. Assert results
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
                Assert.AreEqual("MyCPMDotnet7Solution", solutionContainer.Name);
                
                var projectContainer = solutionContainer.Children[0];
                Assert.AreEqual(projectContainer.Type, "Project");
                Assert.AreEqual("ProjectA", projectContainer.Name);
                
                Assert.IsNotNull(projectContainer.Dependencies);
                var dependencies = projectContainer.Dependencies;
                Assert.AreEqual(1, dependencies.Count);
                var dependency = dependencies.Single();
                Assert.AreEqual("Newtonsoft.Json", dependency.Name);
                Assert.AreEqual("12.0.3", dependency.Version);
                
                // Assert console output
                string output = stringWriter.ToString();
                Assert.IsTrue(output.Contains("Using assets json file:"));
                originalOut.Write(stringWriter.ToString());
            }
            catch
            {
                _testFailed = true;
                throw;
            }
            finally
            {
                Console.SetOut(originalOut);
                env.Cleanup();
            }
        }*/


        // [TestMethod]
        // public void TestSolution_DotNet8_XMLResolver() 
        // {
        //     // 1. Set up environment with .NET 8 (nuget v6.11.1.2)
        //     var dotnetVersion = "8.0.414";
        //     var env = new TestEnvironmentManager().SetupEnvironment(dotnetVersion, "dotnet8");
        //
        //     // 2. Create .NET 8 solution
        //     var builder = new TestSolutionBuilder(env)
        //         .CreateSolution("MySimpleDotnet8Solution")
        //         .CreateAndAddProject("ProjectA")
        //         // Add them manually because dotnet8 doesn't allow adding duplicate PackageReference via CLI
        //         .AddDependencyToProject("ProjectA", "Newtonsoft.Json", "13.0.3")
        //         .RemoveBuildArtifacts() // So we can force using ProjectReferenceResolver instead of assets file
        //         .Build();
        //
        //
        //     // 3. Run inspector
        //     // Redirect console output for assertions later
        //     var stringWriter = new StringWriter();
        //     var originalOut = Console.Out;
        //     Console.SetOut(stringWriter);
        //
        //     var options = new InspectionOptions()
        //     {
        //         TargetPath = builder,
        //         Verbose = true,
        //         PackagesRepoUrl = "https://api.nuget.org/v3/index.json",
        //         OutputDirectory = env.WorkingDirectory,
        //         IgnoreFailure = false
        //     };
        //
        //     try
        //     {
        //         var inspection = InspectorExecutor.ExecuteInspectors(options);
        //
        //         // 4. Assert inspection results
        //         Assert.IsTrue(inspection.Success);
        //         var inspectionResults = inspection.Results;
        //         Assert.IsNotNull(inspectionResults);
        //         Assert.AreEqual(1, inspectionResults.Count);
        //         var result = inspectionResults[0];
        //         Assert.AreEqual(InspectionResult.ResultStatus.Success, result.Status);
        //         Assert.IsNotNull(result.Containers);
        //         Assert.AreEqual(1, result.Containers.Count);
        //         var solutionContainer = result.Containers[0];
        //         Assert.AreEqual(solutionContainer.Type, "Solution");
        //         Assert.AreEqual("MySimpleDotnet8Solution", solutionContainer.Name);
        //
        //         var projectContainer = solutionContainer.Children[0];
        //         Assert.AreEqual(projectContainer.Type, "Project");
        //         Assert.AreEqual("ProjectA", projectContainer.Name);
        //
        //         Assert.IsNotNull(projectContainer.Dependencies);
        //         var dependencies = projectContainer.Dependencies;
        //         Assert.AreEqual(1, dependencies.Count);
        //         var dependency = dependencies.Single();
        //         Assert.AreEqual("Newtonsoft.Json", dependency.Name);
        //         Assert.AreEqual("13.0.3", dependency.Version);
        //
        //         // Assert console output
        //         string output = stringWriter.ToString();
        //         Assert.IsTrue(output.Contains("Using backup XML resolver."));
        //         originalOut.Write(stringWriter.ToString());
        //     }
        //     catch
        //     {
        //         _testFailed = true;
        //         throw;
        //     }
        //     finally
        //     {
        //         // Undo redirect, go back to writing to standard out
        //         Console.SetOut(originalOut);
        //         env.Cleanup();
        //     }
        // }
    }
}