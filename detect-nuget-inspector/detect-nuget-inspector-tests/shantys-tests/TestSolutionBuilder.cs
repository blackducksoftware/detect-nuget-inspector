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
                Console.WriteLine($"✗ Test solution creation failed: {ex.Message}");
                _environment.Cleanup();
                throw;
            }

        }

        public TestSolutionBuilder CreateAndAddProject(string projectName)
        {
            try {
                // Create a minimal project with a class
                RunDotNetCommand($"new classlib -n {projectName}", _solutionDirectory);

                // Add ProjectA to solution
                RunDotNetCommand("sln add ProjectA/ProjectA.csproj", _solutionDirectory);
                return this;
            } catch (Exception ex)
            {
                Console.WriteLine($"✗ Test subproject creation failed: {ex.Message}");
                _environment.Cleanup();
                throw;
            }
        }
        
        public TestSolutionBuilder AddDependencyToProject(string projectName, string packageName, string version)
        {
            try {
                var projectDir = Path.Combine(_solutionDirectory, projectName);
                var args = $"add package {packageName} --version {version}";
                RunDotNetCommand(args, projectDir);
                return this;
            } catch (Exception ex)
            {
                Console.WriteLine($"✗ Adding dependency to subproject failed: {ex.Message}");
                _environment.Cleanup();
                throw;
            }
        }

        public TestSolutionBuilder AddPackageReferenceToCsprojManually(string projectName, string packageName, string version)
        {
            // Adding the same package with a different version will cause the dotnet add package command to update the
            // existing reference to the new version, not add a duplicate. So we need to manually update the project file.
            var projectDir = Path.Combine(_solutionDirectory, projectName);
            var csprojPath = Path.Combine(projectDir, $"{projectName}.csproj");
            var csprojContent = File.ReadAllText(csprojPath);
            var packageReference = $"  <PackageReference Include=\"{packageName}\" Version=\"{version}\" />";
            csprojContent = csprojContent.Replace("</ItemGroup>", $"{packageReference}\n  </ItemGroup>");
            File.WriteAllText(csprojPath, csprojContent);

            return this;
        }

        public TestSolutionBuilder NoBuildArtifacts()
        {
            try
            {
                // Remove bin and obj directories
                foreach (var dir in Directory.GetDirectories(_solutionDirectory, "*", SearchOption.AllDirectories))
                {
                    if (dir.EndsWith("bin") || dir.EndsWith("obj"))
                    {
                        Directory.Delete(dir, true);
                    }
                }

                return this;
            } catch (Exception ex)
            {
                Console.WriteLine($"✗ Removing build artifacts failed: {ex.Message}");
                _environment.Cleanup();
                throw;
            }
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