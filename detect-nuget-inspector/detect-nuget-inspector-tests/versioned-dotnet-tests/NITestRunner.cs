using System;
using System.Collections.Generic;
using System.IO;

namespace DetectNugetInspectorTests.ShantysTests
{
    public class NITestRunner
    {
        private List<string> _executedBranches = new List<string>();

        public NIResult RunBasicSetupTest(string dotnetVersion, string solutionName, string dotnetCommand)
        {
            Console.WriteLine($"Starting basic test with .NET version: {dotnetVersion} using command: {dotnetCommand}");
            
            // 1. Setup environment
            var env = new TestEnvironmentManager().SetupEnvironment(dotnetVersion, dotnetCommand);

            // 2. Create simple solution
            var projectBuilder = new TestSolutionBuilder(env);
            string solutionPath;
            try
            {
                solutionPath = projectBuilder
                    .CreateSolution(solutionName)
                    .Build();
                Console.WriteLine($"✓ Solution created successfully at: {solutionPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Solution creation failed: {ex.Message}");
                env.Cleanup();
                throw;
            }

            // 3. Verify what was created
            try
            {
                VerifyCreatedStructure(solutionPath, solutionName);
                Console.WriteLine("✓ Solution structure verification passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Solution structure verification failed: {ex.Message}");
                env.Cleanup();
                throw;
            }

            // 4. Cleanup
            try
            {
                env.Cleanup();
                Console.WriteLine("✓ Cleanup completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Cleanup warning: {ex.Message}");
            }

            return new NIResult
            {
                Success = true,
                Message = $"Basic setup test completed successfully for .NET {dotnetVersion} using {dotnetCommand}",
                SolutionPath = solutionPath
            };
        }

        private void VerifyCreatedStructure(string solutionPath, string solutionName)
        {
            // Check solution file exists
            var solutionFile = Path.Combine(solutionPath, $"{solutionName}.sln");
            if (!File.Exists(solutionFile))
                throw new InvalidOperationException($"Solution file not found: {solutionFile}");

            // Check Project1 directory exists
            var project1Dir = Path.Combine(solutionPath, "Project1");
            if (!Directory.Exists(project1Dir))
                throw new InvalidOperationException($"Project1 directory not found: {project1Dir}");

            // Check Project1.csproj exists
            var project1File = Path.Combine(project1Dir, "Project1.csproj");
            if (!File.Exists(project1File))
                throw new InvalidOperationException($"Project1.csproj not found: {project1File}");

            Console.WriteLine($"  - Solution file: {solutionFile}");
            Console.WriteLine($"  - Project1 directory: {project1Dir}");
            Console.WriteLine($"  - Project1.csproj: {project1File}");
        }

        public List<string> GetExecutedBranches()
        {
            return new List<string>(_executedBranches);
        }
    }

    public class NIResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string SolutionPath { get; set; }
        // Keep the old properties for when we add back NI inspection
        public object Container { get; set; }
        public object Dependencies { get; set; }
        public object Packages { get; set; }
    }

    public class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }
}