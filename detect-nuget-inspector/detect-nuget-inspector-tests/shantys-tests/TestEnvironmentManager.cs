using System;
using System.IO;
using System.Diagnostics;

namespace DetectNugetInspectorTests.ShantysTests
{
    public class TestEnvironmentManager
    {
        public string DotNetVersion { get; private set; }
        public string DotNetCommand { get; private set; }  // New property
        public string WorkingDirectory { get; private set; }

        public TestEnvironmentManager SetupEnvironment(string dotnetVersion, string dotnetCommand = "dotnet")
        {
            DotNetVersion = dotnetVersion;
            DotNetCommand = dotnetCommand;  // Store the command to use
            WorkingDirectory = Path.Combine(Path.GetTempPath(), "NI-Tests", Guid.NewGuid().ToString());
            
            Directory.CreateDirectory(WorkingDirectory);
            
            // Verify dotnet version is available with the specified command
            ValidateDotNetVersion(dotnetVersion, dotnetCommand);
            
            return this;
        }

        private void ValidateDotNetVersion(string version, string command)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,  // Use the specified command
                    Arguments = "--list-sdks",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (!output.Contains(version))
            {
                throw new InvalidOperationException($"Required .NET SDK version {version} is not available with command '{command}'.");
            }
        }

        public void Cleanup()
        {
            if (Directory.Exists(WorkingDirectory))
            {
                Directory.Delete(WorkingDirectory, recursive: true);
            }
        }
    }
}