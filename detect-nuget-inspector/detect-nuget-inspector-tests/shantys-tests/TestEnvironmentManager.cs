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
            DotNetVersion = dotnetVersion;
            DotNetCommand = ResolveDotNetCommand(desiredDotnetCommand);  // Resolve to actual executable path, will need to be changed/generalized so this works in jenkins 
            WorkingDirectory = Path.Combine(Path.GetTempPath(), "NI-Tests", Guid.NewGuid().ToString());
            
            Directory.CreateDirectory(WorkingDirectory);
            
            // Validate and log .NET version
            ValidateAndLogVersions(dotnetVersion, DotNetCommand);
            
            return this;
        }

        private string ResolveDotNetCommand(string command)
        {
            // The build machine has symlinks for dotnet3,5 and 6. This method will need to be made more robust before being added to jenkins pipeline to just find the desired version on the system if it exists. And maybe not expect installations to be in certain directories.
            switch (command)
            {
                case "dotnet6":
                    return "~/.dotnet/dotnet".Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                case "dotnet7":
                    return "~/.dotnet7/dotnet".Replace("~", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                default:
                    return "dotnet"; // Default dotnet with no alias (6)
            }
        }

        private void ValidateAndLogVersions(string expectedVersion, string command)
        {
            Console.WriteLine($"🔍 Validating environment with command: {command}");
            
            // Check .NET version
            var dotnetVersionResult = RunCommand(command, "--version");
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

        private (int ExitCode, string Output, string Error) RunCommand(string command, string arguments)
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