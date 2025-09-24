using System;
using System.Diagnostics;
using System.IO;

namespace DetectNugetInspectorTests.ShantysTests
{
    public class TestProjectBuilder
    {
        private readonly TestEnvironmentManager _environment;
        private string _solutionDirectory;
        private string _solutionName;

        public TestProjectBuilder(TestEnvironmentManager environment)
        {
            _environment = environment;
        }

        public TestProjectBuilder CreateSimpleSolution(string solutionName = "TestSolution")
        {
            _solutionName = solutionName;
            _solutionDirectory = Path.Combine(_environment.WorkingDirectory, solutionName);
            Directory.CreateDirectory(_solutionDirectory);

            // Create solution
            var createSolutionProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"new sln -n {solutionName}",
                    WorkingDirectory = _solutionDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            createSolutionProcess.Start();
            createSolutionProcess.WaitForExit();

            if (createSolutionProcess.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to create solution: {createSolutionProcess.StandardError.ReadToEnd()}");
            }

            // Create Project1
            var createProjectProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "new console -n Project1",
                    WorkingDirectory = _solutionDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            createProjectProcess.Start();
            createProjectProcess.WaitForExit();

            if (createProjectProcess.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to create Project1: {createProjectProcess.StandardError.ReadToEnd()}");
            }

            // Add Project1 to solution
            var addProjectProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "sln add Project1/Project1.csproj",
                    WorkingDirectory = _solutionDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            addProjectProcess.Start();
            addProjectProcess.WaitForExit();

            if (addProjectProcess.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to add Project1 to solution: {addProjectProcess.StandardError.ReadToEnd()}");
            }

            return this;
        }

        public string Build()
        {
            return _solutionDirectory;
        }
    }
}