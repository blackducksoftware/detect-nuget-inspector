﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Blackduck.Detect.Nuget.Inspector.Inspection.Util;
using Blackduck.Detect.Nuget.Inspector.Model;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.PackagesConfig;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project;

namespace Blackduck.Detect.Nuget.Inspector.Inspection.Inspectors
{
    class ProjectInspector : IInspector
    {
        public ProjectInspectionOptions Options;
        public NugetSearchService NugetService;
        private HashSet<PackageId> CentrallyManagedPackages;
        private bool CheckVersionOverride;
        private string ProjectArtifactsDirectory;

        public ProjectInspector(ProjectInspectionOptions options, NugetSearchService nugetService)
        {
            Options = options;
            NugetService = nugetService;
            if (Options == null)
            {
                throw new Exception("Must provide a valid options object.");
            }

            if (String.IsNullOrWhiteSpace(Options.ProjectDirectory))
            {
                Options.ProjectDirectory = Directory.GetParent(Options.TargetPath).FullName;
            }

            if (String.IsNullOrWhiteSpace(Options.PackagesConfigPath))
            {
                Options.PackagesConfigPath = CreateProjectPackageConfigPath(Options.ProjectDirectory);
            }

            if (String.IsNullOrWhiteSpace(Options.ProjectJsonPath))
            {
                Options.ProjectJsonPath = CreateProjectJsonPath(Options.ProjectDirectory);
            }

            if (String.IsNullOrWhiteSpace(Options.ProjectJsonLockPath))
            {
                Options.ProjectJsonLockPath = CreateProjectJsonLockPath(Options.ProjectDirectory);
            }

            if (String.IsNullOrWhiteSpace(Options.ProjectName))
            {
                Options.ProjectName = Path.GetFileNameWithoutExtension(Options.TargetPath);
            }

            if (String.IsNullOrWhiteSpace(Options.ProjectAssetsJsonPath))
            {
                Options.ProjectAssetsJsonPath = CreateProjectAssetsJsonPath(Options.ProjectDirectory);
            }

            if (String.IsNullOrWhiteSpace(Options.ProjectUniqueId))
            {
                Options.ProjectUniqueId = Path.GetFileNameWithoutExtension(Options.TargetPath);
            }

            if (String.IsNullOrWhiteSpace(Options.VersionName))
            {
                Options.VersionName = InspectorUtil.GetProjectAssemblyVersion(Options.ProjectDirectory);
            }
            
            if (String.IsNullOrWhiteSpace(Options.DirectoryPackagesPropsPath))
            {
                Options.DirectoryPackagesPropsPath = CreateDirectoryPackagesPropsPath(Options.ProjectDirectory);
            }
        }
        
        public ProjectInspector(ProjectInspectionOptions options, NugetSearchService nugetService, HashSet<PackageId> packages, bool checkVersionOverride): this(options, nugetService)
        {
            CentrallyManagedPackages = packages;
            CheckVersionOverride = checkVersionOverride;
        }

        public InspectionResult Inspect()
        {

            try
            {
                Container container = GetContainer();
                List<Container> containers = new List<Container>() { container };
                return new InspectionResult()
                {
                    Status = InspectionResult.ResultStatus.Success,
                    ResultName = Options.ProjectUniqueId,
                    OutputDirectory = Options.OutputDirectory,
                    Containers = containers
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}", ex.ToString());
                if (Options.IgnoreFailure)
                {
                    Console.WriteLine("Error collecting dependencyinformation on project {0}, cause: {1}", Options.ProjectName, ex);
                    return new InspectionResult()
                    {
                        Status = InspectionResult.ResultStatus.Success,
                        ResultName = Options.ProjectUniqueId,
                        OutputDirectory = Options.OutputDirectory
                    };
                }
                else
                {
                    return new InspectionResult()
                    {
                        Status = InspectionResult.ResultStatus.Error,
                        Exception = ex
                    };
                }
            }

        }

        public Container GetContainer()
        {
            bool ProjectAssetsJsonResolved = false;
            if (IsExcluded())
            {
                Console.WriteLine("Project {0} excluded from task", Options.ProjectName);
                return null;
            }
            else
            {
                //TODO: Move to a new class? ResolutionDispatch?
                var stopWatch = Stopwatch.StartNew();
                Console.WriteLine("Processing Project: {0}", Options.ProjectName);
                if (Options.ProjectDirectory != null)
                {
                    Console.WriteLine("Using Project Directory: {0}", Options.ProjectDirectory);
                }
                Container projectNode = new Container();
                projectNode.Name = Options.ProjectUniqueId;
                projectNode.Version = Options.VersionName;
                projectNode.SourcePath = Options.TargetPath;
                projectNode.Type = "Project";


                bool packagesConfigExists = !String.IsNullOrWhiteSpace(Options.PackagesConfigPath) && File.Exists(Options.PackagesConfigPath);
                bool projectJsonExists = !String.IsNullOrWhiteSpace(Options.ProjectJsonPath) && File.Exists(Options.ProjectJsonPath);
                bool projectJsonLockExists = !String.IsNullOrWhiteSpace(Options.ProjectJsonLockPath) && File.Exists(Options.ProjectJsonLockPath);
                bool projectAssetsJsonExists = !String.IsNullOrWhiteSpace(Options.ProjectAssetsJsonPath) && File.Exists(Options.ProjectAssetsJsonPath);
                bool directoryPackagesPropsExists = !String.IsNullOrWhiteSpace(Options.DirectoryPackagesPropsPath) &&
                                                    File.Exists(Options.DirectoryPackagesPropsPath);

                if (directoryPackagesPropsExists)
                {
                    Console.WriteLine("Using Central Package Management: " + Options.DirectoryPackagesPropsPath);
                    var packagesPropertyLoader =
                        new SolutionDirectoryPackagesPropertyLoader(Options.DirectoryPackagesPropsPath, Options.ExcludedDependencyTypes, CentrallyManagedPackages);
                    projectNode.PackagePropertyPackages = packagesPropertyLoader.Process();
                    projectNode.Dependencies = packagesPropertyLoader.GetGlobalPackageReferences().ToHashSet();
                }
                else if (packagesConfigExists)
                {
                    Console.WriteLine("Using packages config: " + Options.PackagesConfigPath);
                    var packagesConfigResolver = new PackagesConfigResolver(Options.PackagesConfigPath, NugetService, Options.ExcludedDependencyTypes);
                    var packagesConfigResult = packagesConfigResolver.Process();
                    projectNode.Packages = packagesConfigResult.Packages;
                    projectNode.Dependencies = packagesConfigResult.Dependencies;
                }
                else if (projectJsonLockExists)
                {
                    Console.WriteLine("Using json lock: " + Options.ProjectJsonLockPath);
                    var projectJsonLockResolver = new ProjectLockJsonResolver(Options.ProjectJsonLockPath, Options.ExcludedDependencyTypes);
                    var projectJsonLockResult = projectJsonLockResolver.Process();
                    projectNode.Packages = projectJsonLockResult.Packages;
                    projectNode.Dependencies = projectJsonLockResult.Dependencies;
                }
                else if (projectAssetsJsonExists)
                {
                    Console.WriteLine("Using assets json file: " + Options.ProjectAssetsJsonPath);
                    var projectAssetsJsonResolver = new ProjectAssetsJsonResolver(Options.ProjectAssetsJsonPath, Options.ExcludedDependencyTypes);
                    var projectAssetsJsonResult = projectAssetsJsonResolver.Process();
                    projectNode.Packages = projectAssetsJsonResult.Packages;
                    projectNode.Dependencies = projectAssetsJsonResult.Dependencies;
                    ProjectAssetsJsonResolved = true;
                }
                else if (projectJsonExists)
                {
                    Console.WriteLine("Using project json: " + Options.ProjectJsonPath);
                    var projectJsonResolver = new ProjectJsonResolver(Options.ProjectName, Options.ProjectJsonPath);
                    var projectJsonResult = projectJsonResolver.Process();
                    projectNode.Packages = projectJsonResult.Packages;
                    projectNode.Dependencies = projectJsonResult.Dependencies;
                }
                else
                {
                    Console.WriteLine("Attempting reference resolver: " + Options.TargetPath);
                    ProjectReferenceResolver referenceResolver;
                    if (CentrallyManagedPackages != null && CentrallyManagedPackages.Count > 0)
                    {
                        referenceResolver = new ProjectReferenceResolver(Options.TargetPath, NugetService, Options.ExcludedDependencyTypes, CentrallyManagedPackages, CheckVersionOverride);
                    }
                    else
                    {
                        referenceResolver = new ProjectReferenceResolver(Options.TargetPath, NugetService, Options.ExcludedDependencyTypes);
                    }
                    var projectReferencesResult = referenceResolver.Process();
                    if (projectReferencesResult.Success)
                    {
                        Console.WriteLine("Reference resolver succeeded.");
                        projectNode.Packages = projectReferencesResult.Packages;
                        projectNode.Dependencies = projectReferencesResult.Dependencies;
                    }
                    else
                    {
                        Console.WriteLine("Using backup XML resolver.");
                        ProjectXmlResolver xmlResolver;
                        if (CentrallyManagedPackages != null && CentrallyManagedPackages.Count > 0)
                        {
                            xmlResolver = new ProjectXmlResolver(Options.TargetPath, NugetService, Options.ExcludedDependencyTypes, CentrallyManagedPackages, CheckVersionOverride);
                        }
                        else
                        {
                            xmlResolver = new ProjectXmlResolver(Options.TargetPath, NugetService, Options.ExcludedDependencyTypes);
                        }
                        var xmlResult = xmlResolver.Process();
                        projectNode.Version = xmlResult.ProjectVersion;
                        projectNode.Packages = xmlResult.Packages;
                        projectNode.Dependencies = xmlResult.Dependencies;
                    }
                }

                if(!ProjectAssetsJsonResolved) {
                    var projectAssetsJsonPathFromProperty = GetProjectAssetsJsonPathFromNugetProperty(Options.ProjectDirectory, Options.ProjectName);
                    if (!String.IsNullOrWhiteSpace(projectAssetsJsonPathFromProperty)
                        && File.Exists(projectAssetsJsonPathFromProperty))
                    {
                        Console.WriteLine("Using assets json file configured in property file: " + projectAssetsJsonPathFromProperty);
                        var projectAssetsJsonResolver = new ProjectAssetsJsonResolver(projectAssetsJsonPathFromProperty, Options.ExcludedDependencyTypes);
                        var projectAssetsJsonResult = projectAssetsJsonResolver.Process();
                        projectNode.Packages.UnionWith(projectAssetsJsonResult.Packages);
                        projectNode.Dependencies.UnionWith(projectAssetsJsonResult.Dependencies);
                    }
                }
 
                if (projectNode != null && projectNode.Dependencies != null && projectNode.Packages != null)
                {
                    Console.WriteLine("Found {0} dependencies among {1} packages.", projectNode.Dependencies.Count, projectNode.Packages.Count);
                }
                Console.WriteLine("Finished processing project {0} which took {1} ms.", Options.ProjectName, stopWatch.ElapsedMilliseconds);

                return projectNode;
            }
        }

        public bool IsExcluded()
        {
            if (String.IsNullOrWhiteSpace(Options.IncludedModules) && String.IsNullOrWhiteSpace(Options.ExcludedModules))
            {
                return false;
            };

            String projectName = Options.ProjectName.Trim();
            if (!String.IsNullOrWhiteSpace(Options.IncludedModules))
            {
                ISet<string> includedSet = new HashSet<string>();
                string[] projectPatternArray = Options.IncludedModules.Split(new char[] { ',' });
                foreach (string projectPattern in projectPatternArray)
                {
                    if (projectPattern.Trim() == projectName) // legacy behaviour, match if equals with trim.
                    {
                        return false;
                    }
                    try
                    {
                        Match patternMatch = Regex.Match(projectName, projectPattern, RegexOptions.None, TimeSpan.FromMinutes(1));
                        if (patternMatch.Success)
                        {
                            return false;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unable to parse " + projectPattern + " as a regular expression, so pattern matching module names could not occur.");
                        Console.WriteLine("It is still compared to the project name. To use it as a pattern please fix the following issue:");
                        Console.WriteLine(e);
                    }
                }
                return true;//did not match any inclusion, exclude it.
            }
            else
            {
                ISet<string> excludedSet = new HashSet<string>();
                string[] projectPatternArray = Options.ExcludedModules.Split(new char[] { ',' });
                foreach (string projectPattern in projectPatternArray)
                {
                    if (projectPattern.Trim() == projectName) // legacy behaviour, match if equals with trim.
                    {
                        return true;
                    }
                    try
                    {
                        Match patternMatch = Regex.Match(projectName, projectPattern, RegexOptions.None, TimeSpan.FromMinutes(1));
                        if (patternMatch.Success)
                        {
                            return true;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Unable to parse " + projectPattern + " as a regular expression, so pattern matching module names could not occur.");
                        Console.WriteLine("It is still compared to the project name. To use it as a pattern please fix the following issue:");
                        Console.WriteLine(e);
                    }
                }
                return false;//did not match exclusion, include it.
            }
        }


        private string CreateProjectPackageConfigPath(string projectDirectory)
        {
            return PathUtil.Combine(projectDirectory, "packages.config");
        }

        private string CreateProjectJsonPath(string projectDirectory)
        {
            return PathUtil.Combine(projectDirectory, "project.json");
        }

        private string CreateProjectJsonLockPath(string projectDirectory)
        {
            return PathUtil.Combine(projectDirectory, "project.lock.json");
        }

        private string CreateProjectAssetsJsonPath(string projectDirectory)
        {
            if (!String.IsNullOrWhiteSpace(Options.ArtifactsPath))
            {
                if (Directory.Exists(Options.ArtifactsPath))
                {
                    return FindProjectArtifactsFolder();
                }
                Console.WriteLine("The Artifacts Path is invalid or Detect does not have appropriate permissions. Please specify a valid path.");
            }
            return PathUtil.Combine(projectDirectory, "obj", "project.assets.json");
        }
        
        private string CreateDirectoryPackagesPropsPath(string projectDirectory)
        {
            return PathUtil.Combine(projectDirectory, "Directory.Packages.props");
        }

        private string CreateProjectNugetgPropertyPath(string projectDirectory, string projectName)
        {
            return PathUtil.Combine(projectDirectory, "obj", projectName + ".csproj.nuget.g.props");
        }

        private string GetProjectAssetsJsonPathFromNugetProperty(string projectDirectory, string projectName)
        {
            if (!String.IsNullOrWhiteSpace(Options.ArtifactsPath))
            {
                return CreateProjectAssetsJsonPath(projectDirectory);
            }
            string projectNugetgPropertyPath = CreateProjectNugetgPropertyPath(projectDirectory, projectName);
            bool projectNugetgPropertyExists = !String.IsNullOrWhiteSpace(projectNugetgPropertyPath) && File.Exists(projectNugetgPropertyPath);
            if (projectNugetgPropertyExists)
            {
                Console.WriteLine("Using project nuget property file: " + projectNugetgPropertyPath);
                var xmlResolver = new ProjectNugetgPropertyLoader(projectNugetgPropertyPath, NugetService);
                return xmlResolver.Process();
            }
            return null;
        }

        private string FindProjectArtifactsFolder()
        {
            if (File.Exists(Path.Combine(Options.ArtifactsPath, "obj", Options.ProjectName, "project.assets.json")))
            {
                return Path.Combine(Options.ArtifactsPath, "obj", Options.ProjectName, "project.assets.json");
            }
            
            ParseThroughDirectories(Options.ArtifactsPath);

            if (String.IsNullOrWhiteSpace(ProjectArtifactsDirectory))
            {
                Console.WriteLine("Could not find project.assets.json for this project, please check the artifacts directory provided and try again.");
                return null;
            }

            return Path.Combine(ProjectArtifactsDirectory, "project.assets.json");
        }

        private void ParseThroughDirectories(string path)
        {
            string[] directories = Directory.GetDirectories(path);

            if (directories.Length == 0)
            {
                return;
            }
            
            foreach (string directory in directories)
            {
                if (!directory.EndsWith("bin") && !directory.EndsWith("debug"))
                {
                    if (File.Exists(Path.Combine(directory, "project.assets.json")) && directory.Contains(Options.ProjectName))
                    { 
                        ProjectArtifactsDirectory = directory;
                        break;
                    }
                    ParseThroughDirectories(directory);
                }
            }
        }
    }
}
