using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synopsys.Detect.Nuget.Inspector.Inspection;

namespace Synopsys.Detect.Nuget.Inspector.Configuration
{
    public class OptionParser
    {
        public static ParsedOptions ParseOptions(string[] args)
        {
            InspectionOptions options = null;
            try
            {
                CommandLineRunOptionsParser parser = new CommandLineRunOptionsParser();
                RunOptions parsedOptions = parser.ParseArguments(args);

                if (parsedOptions == null)
                {
                    return ParsedOptions.Failed();
                }

                if (!string.IsNullOrWhiteSpace(parsedOptions.AppSettingsFile))
                {
                    RunOptions appOptions = parser.LoadAppSettings(parsedOptions.AppSettingsFile);
                    parsedOptions.Override(appOptions);
                }

                if (string.IsNullOrWhiteSpace(parsedOptions.TargetPath))
                {
                    parsedOptions.TargetPath = Directory.GetCurrentDirectory();
                }

                options = new InspectionOptions()
                {
                    ExcludedModules = parsedOptions.ExcludedModules,
                    IncludedModules = parsedOptions.IncludedModules,
                    IgnoreFailure = parsedOptions.IgnoreFailures == "true",
                    OutputDirectory = parsedOptions.OutputDirectory,
                    PackagesRepoUrl = parsedOptions.PackagesRepoUrl,
                    NugetConfigPath = parsedOptions.NugetConfigPath,
                    TargetPath = parsedOptions.TargetPath,
                    Verbose = parsedOptions.Verbose
                };

                return ParsedOptions.Succeeded(options);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to parse options.");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                return ParsedOptions.Failed();
            }
        }
    }

    public class ParsedOptions
    {
        public InspectionOptions Options;
        public bool Success = false;
        public bool IgnoreFailures = false;
        public int ExitCode = 0;

        public static ParsedOptions Failed(int exitCode = -1)
        {
            return new ParsedOptions()
            {
                Success = false,
                ExitCode = exitCode
            };
        }

        public static ParsedOptions Succeeded(InspectionOptions options)
        {
            return new ParsedOptions
            {
                Success = true,
                Options = options
            };
        }
    }
}
