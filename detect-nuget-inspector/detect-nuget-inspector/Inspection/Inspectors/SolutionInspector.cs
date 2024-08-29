using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Graph;
using NuGet.Packaging;
using SCA.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using SCA.Detect.Nuget.Inspector.Inspection.Util;
using SCA.Detect.Nuget.Inspector.Model;

namespace SCA.Detect.Nuget.Inspector.Inspection.Inspectors
{
    class SolutionInspector : IInspector
    {
        public SolutionInspectionOptions Options;
        public NugetSearchService NugetService;

        public SolutionInspector(SolutionInspectionOptions options, NugetSearchService nugetService)
        {
            Options = options;
            NugetService = nugetService;
            if (Options == null)
            {
                throw new Exception("Must provide a valid options object.");
            }

            if (String.IsNullOrWhiteSpace(Options.OutputDirectory))
            {
                string currentDirectory = Directory.GetCurrentDirectory();
                Options.OutputDirectory = PathUtil.Combine(currentDirectory, InspectorUtil.DEFAULT_OUTPUT_DIRECTORY);
            }
            if (String.IsNullOrWhiteSpace(Options.SolutionName))
            {
                Options.SolutionName = Path.GetFileNameWithoutExtension(Options.TargetPath);
            }
        }

        public InspectionResult Inspect()
        {
            try
            {
                return new InspectionResult()
                {
                    Status = InspectionResult.ResultStatus.Success,
                    ResultName = Options.SolutionName,
                    OutputDirectory = Options.OutputDirectory,
                    Containers = new List<Container>() { GetContainer() }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine("{0}", ex.ToString());
                return new InspectionResult()
                {
                    Status = InspectionResult.ResultStatus.Error,
                    Exception = ex
                };
            }

        }

        public Container GetContainer()
        {
            Console.WriteLine("Processing Solution: " + Options.TargetPath);
            var stopwatch = Stopwatch.StartNew();
            Container solution = new Container();
            solution.Name = Options.SolutionName;
            solution.SourcePath = Options.TargetPath;
            solution.Type = "Solution";
            try
            {
                HashSet<PackageId> packagesProperty = new HashSet<PackageId>();
                HashSet<PackageId> globalPackageReferences = new HashSet<PackageId>();
                string parentDirectory = Directory.GetParent(solution.SourcePath).FullName;
                
                string solutionDirectoryPackagesPropertyPath = CreateSolutionDirectoryPackagesPropertyPath(parentDirectory);
                bool solutionDirectoryPackagesPropertyExists = !String.IsNullOrWhiteSpace(solutionDirectoryPackagesPropertyPath) && File.Exists(solutionDirectoryPackagesPropertyPath);
                bool checkVersionOverride = true;
                if (solutionDirectoryPackagesPropertyExists)
                {
                    Console.WriteLine("Using solution directory packages property file: " + solutionDirectoryPackagesPropertyPath);
                    var packagePropertyLoader = new SolutionDirectoryPackagesPropertyLoader(solutionDirectoryPackagesPropertyPath, Options.ExcludedDependencyTypes);
                    packagesProperty = packagePropertyLoader.Process();
                    globalPackageReferences = packagePropertyLoader.GetGlobalPackageReferences();
                    checkVersionOverride = packagePropertyLoader.GetVersionOverrideEnabled();
                }

                HashSet<PackageId> buildPropertyPackages = new HashSet<PackageId>();
                
                string solutionDirectoryBuildPropertyPath = CreateSolutionDirectoryBuildPropertyPath(parentDirectory);
                bool solutionDirectoryBuildPropertyExists = !String.IsNullOrWhiteSpace(solutionDirectoryBuildPropertyPath) && File.Exists(solutionDirectoryBuildPropertyPath);
                if (solutionDirectoryBuildPropertyExists)
                {
                    Console.WriteLine("Using solution directory build property file: " + solutionDirectoryBuildPropertyPath);
                    var propertyLoader = new SolutionDirectoryBuildPropertyLoader(solutionDirectoryBuildPropertyPath, NugetService,packagesProperty, checkVersionOverride, Options.ExcludedDependencyTypes);
                    buildPropertyPackages = propertyLoader.Process();
                    checkVersionOverride = propertyLoader.GetVersionOverrideEnabled();
                }
                
                List<ProjectFile> projectFiles = FindProjectFilesFromSolutionFile(Options.TargetPath);
                Console.WriteLine("Parsed Solution File");
                if (projectFiles.Count > 0)
                {
                    string solutionDirectory = Path.GetDirectoryName(Options.TargetPath);
                    Console.WriteLine("Solution directory: {0}", solutionDirectory);

                    var duplicateNames = projectFiles
                        .GroupBy(project => project.Name)
                        .Where(group => group.Count() > 1)
                        .Select(group => group.Key);

                    var duplicatePaths = projectFiles
                        .GroupBy(project => project.Path)
                        .Where(group => group.Count() > 1)
                        .Select(group => group.Key);

                    foreach (ProjectFile project in projectFiles)
                    {
                        try
                        {
                            string projectRelativePath = project.Path;
                            string projectPath = null;
                            Boolean directoryPackagesExists = false;
                            if (projectRelativePath.Contains("Directory.Packages.props"))
                            {
                                string parentPath = solutionDirectory;
                                bool fileNotFound = true;
                                while (fileNotFound)
                                {
                                    string checkFile = Path.Combine(parentPath, projectRelativePath);
                                    if (parentPath.Equals("") || parentPath.Equals(Path.GetPathRoot(solutionDirectory)))
                                    {
                                        Console.WriteLine("The Path provided in the sln file is wrong, will skip parsing over this file");
                                        break;
                                    }
                                    parentPath =
                                        parentPath.Substring(0, OperatingSystem.IsWindows() ? parentPath.LastIndexOf("\\") : parentPath.LastIndexOf("/"));
                                    directoryPackagesExists = File.Exists(checkFile);
                                    fileNotFound = !directoryPackagesExists;
                                    projectPath = checkFile;
                                }
                            }
                            else
                            {
                                projectPath = PathUtil.Combine(solutionDirectory, projectRelativePath);
                            }
                            string projectName = project.Name;
                            string projectId = projectName;
                            if (duplicateNames.Contains(projectId))
                            {
                                Console.WriteLine($"Duplicate project name '{projectId}' found. Using GUID instead.");
                                projectId = project.GUID;
                            }
                            Boolean projectFileExists = false;
                            try
                            {
                                projectFileExists = File.Exists(projectPath);
                                if (!projectFileExists && !directoryPackagesExists)
                                {
                                    projectPath = PathUtil.Combine(projectPath, "Directory.Packages.props");
                                    directoryPackagesExists = File.Exists(projectPath);
                                }
                                
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Skipping unknown project path: " + projectPath);
                                continue;
                            }

                            if (!projectFileExists && !directoryPackagesExists)
                            {
                                Console.WriteLine("Skipping non-existent project path: " + projectPath);
                                continue;
                            }

                            ProjectInspector projectInspector = new ProjectInspector(new ProjectInspectionOptions(Options)
                            {
                                ProjectName = projectName,
                                ProjectUniqueId = projectId,
                                TargetPath = projectPath
                            }, NugetService, packagesProperty, checkVersionOverride);

                            InspectionResult projectResult = projectInspector.Inspect();
                            if (projectResult != null && projectResult.Status == InspectionResult.ResultStatus.Success && projectResult.Containers != null)
                            {
                                foreach (Container container in projectResult.Containers)
                                {
                                    if (container != null && container.Dependencies != null && buildPropertyPackages.Count > 0)
                                    {
                                        container.Dependencies.AddRange(buildPropertyPackages);
                                    }
                                    
                                    if (container != null && container.Dependencies != null && globalPackageReferences.Count > 0)
                                    {
                                        container.Dependencies.AddRange(globalPackageReferences);
                                    }

                                    if (container != null && container.PackagePropertyPackages != null && container.PackagePropertyPackages.Count > 0)
                                    {
                                        packagesProperty.AddRange(container.PackagePropertyPackages);
                                    }
                                }
                                solution.Children.AddRange(projectResult.Containers);
                            }
                            else if (projectResult.Status == InspectionResult.ResultStatus.Error)
                            {
                                throw new Exception(projectResult.Exception.ToString());
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                            if (Options.IgnoreFailure)
                            {

                                Console.WriteLine("Error inspecting project: {0}", project.Path);
                                Console.WriteLine("Error inspecting project. Cause: {0}", ex);
                            }
                            else
                            {
                                throw ex;
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("No project data found for solution {0}", Options.TargetPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                if (Options.IgnoreFailure)
                {

                    Console.WriteLine("Error executing Build BOM task. Cause: {0}", ex);
                }
                else
                {
                    throw ex;
                }
            }

            if (solution != null && solution.Children != null)
            {
                Console.WriteLine("Found " + solution.Children.Count + " children.");
            }
            Console.WriteLine("Finished processing solution: " + Options.TargetPath);
            Console.WriteLine("Took " + stopwatch.ElapsedMilliseconds + " ms to process.");
            return solution;
        }

        private List<ProjectFile> FindProjectFilesFromSolutionFile(string solutionPath)
        {
            var projects = new List<ProjectFile>();
            // Visual Studio right now is not resolving the Microsoft.Build.Construction.SolutionFile type
            // parsing the solution file manually for now.
            if (File.Exists(solutionPath))
            {
                List<string> contents = new List<string>(File.ReadAllLines(solutionPath));
                var projectLines = contents.FindAll(text => text.StartsWith("Project("));
                var projectDirectoryPackagesLines = contents.FindAll(text => text.Contains("Directory.Packages.props"));
                foreach (string projectText in projectDirectoryPackagesLines)
                {
                    if (!projectText.Equals("\t\tDirectory.Packages.props = Directory.Packages.props"))
                    {
                        ProjectFile file = ProjectFile.Parse(projectText);
                        if (file != null)
                        { 
                            projects.Add(file);
                        }
                    }
                }
                foreach (string projectText in projectLines)
                {
                    ProjectFile file = ProjectFile.Parse(projectText);
                    if (file != null)
                    { 
                        projects.Add(file);
                    }
                }
                Console.WriteLine("Nuget Inspector found {0} project elements, processed {1} project elements for data", projectLines.Count(), projects.Count());
            }
            else
            {
                throw new System.Exception("Solution File " + solutionPath + " not found");
            }

            return projects;
        }

        private string CreateSolutionDirectoryBuildPropertyPath(string solutionDirectory)
        {
            return PathUtil.Combine(solutionDirectory, "Directory.Build.props");
        }
        
        private string CreateSolutionDirectoryPackagesPropertyPath(string solutionDirectory)
        {
            return PathUtil.Combine(solutionDirectory, "Directory.Packages.props");
        }
    }
}
