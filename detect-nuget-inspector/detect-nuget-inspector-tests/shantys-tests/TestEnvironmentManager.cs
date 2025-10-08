using System;
using System.Diagnostics;
using System.IO;

namespace DetectNugetInspectorTests.ShantysTests
{
    public class TestEnvironmentManager
    {
        public string DotNetVersion { get; private set; }
        public string NuGetVersion { get; private set; }
        public string DotNetCommand { get; private set; }
        public string WorkingDirectory { get; private set; }

        public TestEnvironmentManager SetupEnvironment(string dotnetVersion, string desiredDotnetCommand = "dotnet")
        {
            try
            {
                DotNetVersion = dotnetVersion;
                DotNetCommand = desiredDotnetCommand;  
                WorkingDirectory = Path.Combine(Path.GetTempPath(), "NI-Tests", Guid.NewGuid().ToString());
            
                Directory.CreateDirectory(WorkingDirectory);
            
                // Validate and log .NET and NuGet versions
                ValidateAndLogVersions(dotnetVersion, DotNetCommand);
                
                Console.WriteLine($"✓ Environment setup successful - Working directory: {this.WorkingDirectory}");
                return this;
            } catch (Exception ex)
            {
                Console.WriteLine($"✗ Environment setup failed: {ex.Message}");
                throw;
            }
        }

        private void ValidateAndLogVersions(string expectedVersion, string command)
        {
            Console.WriteLine($"🔍 Validating environment with command: {command}");
            
            // Check .NET version
            var dotnetVersionResult = RunCommand(command, " --version");
            if (dotnetVersionResult.ExitCode != 0)
            {
                throw new InvalidOperationException($"Failed to get .NET version using command '{command}': {dotnetVersionResult.Error}");
            }
            
            var actualDotNetVersion = dotnetVersionResult.Output.Trim();
            Console.WriteLine($"📋 .NET Version: {actualDotNetVersion}");
            
            // Throw exception if the requested version doesn't match what's available
            if (!actualDotNetVersion.StartsWith(expectedVersion))
            {
                Console.WriteLine($"❌ Version mismatch: Expected {expectedVersion}, but got {actualDotNetVersion}");
                throw new InvalidOperationException($"Requested .NET version {expectedVersion} is not available. System returned version {actualDotNetVersion}. Please install the required .NET SDK version and create appropriate alias.");
            }
            
            // Check NuGet version
            var nugetVersionResult = RunCommand(command, "nuget --version");
            if (nugetVersionResult.ExitCode == 0)
            {
                NuGetVersion = nugetVersionResult.Output.Trim();
                Console.WriteLine($"📦 NuGet Version: {NuGetVersion}");
            }
            else
            {
                Console.WriteLine($"⚠️  Could not determine NuGet version: {nugetVersionResult.Error}");
                NuGetVersion = "Unknown";
            }
            
            Console.WriteLine($"📁 Working Directory: {WorkingDirectory}");
            Console.WriteLine("✅ Environment validation complete");
        }

        private (int ExitCode, string Output, string Error) RunCommand(string command, string arguments) // TODO only need one dotnet command runner
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return (process.ExitCode, output, error);
            }
            catch (Exception ex)
            {
                //Console.Error.WriteLine($"❌ Error running command '{command} {arguments}': {ex.Message}");
                return (-1, string.Empty, ex.Message);
            }
        }

        public void Cleanup()
        {
            if (Directory.Exists(WorkingDirectory))
            {
                Directory.Delete(WorkingDirectory, true);
                Console.WriteLine($"🧹 Cleaned up working directory: {WorkingDirectory}");
            }
        
        }
    }
}