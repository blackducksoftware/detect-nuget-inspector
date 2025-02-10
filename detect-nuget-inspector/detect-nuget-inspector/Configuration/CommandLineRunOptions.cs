using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blackduck.Detect.Nuget.Inspector.Configuration
{
    public class RunOptions
    {
        [CommandLineArg(CommandLineArgKeys.AppSettingsFile, "The file path for the application settings that overrides all settings.")]
        public string AppSettingsFile = "";

        [AppConfigArg(AppConfigKeys.TargetPath)]
        [CommandLineArg(CommandLineArgKeys.TargetPath, "The path to the solution or project file to find dependencies")]
        public string TargetPath = "";

        [AppConfigArg(AppConfigKeys.OutputDirectory)]
        [CommandLineArg(CommandLineArgKeys.OutputDirectory, "The directory path to output the dependency node files.")]
        public string OutputDirectory = "";

        [AppConfigArg(AppConfigKeys.ExcludedModules)]
        [CommandLineArg(CommandLineArgKeys.ExcludedModules, "The names of the projects in a solution to exclude from dependency node generation.")]
        public string ExcludedModules = "";

        [AppConfigArg(AppConfigKeys.IncludedModules)]
        [CommandLineArg(CommandLineArgKeys.IncludedModules, "The names of the projects in a solution to include from dependency node generation.")]
        public string IncludedModules = "";

        [AppConfigArg(AppConfigKeys.IgnoreFailures)]
        [CommandLineArg(CommandLineArgKeys.IgnoreFailures, "If true log the error but do not throw an exception.")]
        public string IgnoreFailures = "";

        [AppConfigArg(AppConfigKeys.PackagesRepoUrl)]
        [CommandLineArg(CommandLineArgKeys.PackagesRepoUrl, "The URL of the NuGet repository to get the packages.")]
        public string PackagesRepoUrl = "https://api.nuget.org/v3/index.json";

        [AppConfigArg(AppConfigKeys.NugetConfigPath)]
        [CommandLineArg(CommandLineArgKeys.NugetConfigPath, "The path of a NuGet config file to load package sources from.")]
        public string NugetConfigPath = "";
        
        [AppConfigArg(AppConfigKeys.ExcludedDependencyTypes)]
        [CommandLineArg(CommandLineArgKeys.ExcludedDependencyTypes, "The names of the dependency types to exclude from dependency node generation")]
        public string ExcludedDependencyTypes = "";
        
        [AppConfigArg(AppConfigKeys.ArtifactsPath)]
        [CommandLineArg(CommandLineArgKeys.ArtifactsPath, "The output path to Nuget build artifacts to derive package information from.")]
        public string ArtifactsPath = "";


        public bool ShowHelp;
        public bool Verbose;

        public void Override(RunOptions overide)
        {
            AppSettingsFile = String.IsNullOrEmpty(overide.AppSettingsFile) ? this.AppSettingsFile : overide.AppSettingsFile;
            TargetPath = String.IsNullOrEmpty(overide.TargetPath) ? this.TargetPath : overide.TargetPath;
            OutputDirectory = String.IsNullOrEmpty(overide.OutputDirectory) ? this.OutputDirectory : overide.OutputDirectory;
            ExcludedModules = String.IsNullOrEmpty(overide.ExcludedModules) ? this.ExcludedModules : overide.ExcludedModules;
            IncludedModules = String.IsNullOrEmpty(overide.IncludedModules) ? this.IncludedModules : overide.IncludedModules;
            IgnoreFailures = String.IsNullOrEmpty(overide.IgnoreFailures) ? this.IgnoreFailures : overide.IgnoreFailures;
            PackagesRepoUrl = String.IsNullOrEmpty(overide.PackagesRepoUrl) ? this.PackagesRepoUrl : overide.PackagesRepoUrl;
            NugetConfigPath = String.IsNullOrEmpty(overide.NugetConfigPath) ? this.NugetConfigPath : overide.NugetConfigPath;
            ExcludedDependencyTypes = String.IsNullOrEmpty(overide.ExcludedDependencyTypes)
                ? this.ExcludedDependencyTypes
                : overide.ExcludedDependencyTypes;
            ArtifactsPath = String.IsNullOrEmpty(overide.ArtifactsPath) ? this.ArtifactsPath : overide.ArtifactsPath;
        }
    }
}
