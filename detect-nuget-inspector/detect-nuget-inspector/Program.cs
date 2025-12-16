using Microsoft.Build.Locator;
using Blackduck.Detect.Nuget.Inspector.Configuration;
using Blackduck.Detect.Nuget.Inspector.Inspection;
using System.Reflection;

class Program
{
    public static void Main(string[] args)
    {

        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Console.WriteLine($"Running NuGet Inspector version: {version}");
            Console.WriteLine("Registering MSBuild defaults.");
            VisualStudioInstance registeredInstance = MSBuildLocator.RegisterDefaults();
            Console.WriteLine("MSBuild registered: " + registeredInstance.MSBuildPath + " (Version: " + registeredInstance.Version + ")");
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed to register defaults.");
            Console.Write(e);
        }

        int exitCode = 0;
        bool ignoreFailure = false;

        var options = OptionParser.ParseOptions(args);

        if (options.Success && options.Options != null)
        {
            ignoreFailure = options.Options.IgnoreFailure;

            var execution = InspectorExecutor.ExecuteInspectors(options.Options);

            if (execution.ExitCode != 0)
            {
                exitCode = execution.ExitCode;
            }
        }
        else
        {
            exitCode = options.ExitCode;
        }

        if (ignoreFailure)
        {
            Console.WriteLine("Desired exit code was ignored: " + exitCode.ToString());
            Environment.Exit(0);
        }
        else
        {
            Environment.Exit(exitCode);
        }
    }
}