using System;
using System.Diagnostics;
using System.IO;

namespace DetectNugetInspectorTests.ShantysTests
{
    public class TestSolutionBuilder
    {
        private readonly TestEnvironmentManager _environment;
        private string _solutionDirectory;
        private string _solutionName;

        public TestSolutionBuilder(TestEnvironmentManager environment)
        {
            _environment = environment;
        }

        public TestSolutionBuilder CreateSolution(string solutionName)
        {
            try
            {
                _solutionName = solutionName;
                _solutionDirectory = Path.Combine(_environment.WorkingDirectory, solutionName);
                Directory.CreateDirectory(_solutionDirectory);

                // Create solution
                RunDotNetCommand($"new sln -n {solutionName}", _solutionDirectory);

                return this;
            } catch (Exception ex)
            {
                Console.WriteLine($"✗ Test Solution creation failed: {ex.Message}");
                _environment.Cleanup();
                throw;
            }

        }

        public TestSolutionBuilder CreateAndAddProject(string projectName)
        {
            // Create ProjectA
            RunDotNetCommand($"new console -n {projectName}", _solutionDirectory);

            // Add ProjectA to solution
            RunDotNetCommand("sln add ProjectA/ProjectA.csproj", _solutionDirectory);
            return this;
        }
        
        public TestSolutionBuilder AddDependencyToProject(string projectName, string packageName, string version)
        {
            var projectDir = Path.Combine(_solutionDirectory, projectName);
            var args = $"add package {packageName} --version {version}";
            RunDotNetCommand(args, projectDir);
            return this;
        }
        
        
        private void RunDotNetCommand(string arguments, string workingDirectory)
        {
            var command = $"{_environment.DotNetCommand} {arguments}";
            Console.WriteLine($"> {command}");
            
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _environment.DotNetCommand,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"dotnet {arguments} failed: {process.StandardError.ReadToEnd()}");
            }
        }

        public string Build()
        {
            return _solutionDirectory;
        }
    }
}