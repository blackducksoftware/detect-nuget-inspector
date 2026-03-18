using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Blackduck.Detect.Nuget.Inspector.Inspection.Inspectors;
using Blackduck.Detect.Nuget.Inspector.Inspection.Util;

namespace Blackduck.Detect.Nuget.Inspector.Inspection
{
    //Given a generic InspectionOptions, InspectorDispatch is responsible for instantiating the correct Inspector (Project or Solution)
    class InspectorDispatch
    {

        public InspectorDispatch()
        {
        }

        public List<InspectionResult> Inspect(InspectionOptions options, NugetSearchService nugetService)
        {
            return CreateInspectors(options, nugetService)?.Select(insp => insp.Inspect()).ToList();
        }

        private static string[] FindSolutionFilesTopLevel(string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return Array.Empty<string>();
            return Directory.EnumerateFiles(directoryPath)
                .Where(f => f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        // Warns and gives precedence to .sln over .slnx when both exist with the same base name
        private static string[] FilterOutDuplicateXMLSolutionFiles(string[] solutionPaths)
        {
            // Only bother if at least one .slnx file is present
            if (!solutionPaths.Any(f => f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
            {
                return solutionPaths;
            }
            var slnFiles = solutionPaths.Where(f => f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)).ToList();
            var slnxFiles = solutionPaths.Where(f => f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)).ToList();
            var slnBaseNames = new HashSet<string>(slnFiles.Select(f => Path.GetFileNameWithoutExtension(f)), StringComparer.OrdinalIgnoreCase);
            var slnxBaseNames = new HashSet<string>(slnxFiles.Select(f => Path.GetFileNameWithoutExtension(f)), StringComparer.OrdinalIgnoreCase);
            var duplicateBaseNames = slnBaseNames.Intersect(slnxBaseNames, StringComparer.OrdinalIgnoreCase).ToList();
            if (duplicateBaseNames.Count > 0)
            {
                foreach (var baseName in duplicateBaseNames)
                {
                    Console.WriteLine($"Warning: Both {baseName}.sln and {baseName}.slnx found. Only {baseName}.sln will be processed, {baseName}.slnx will be ignored.");
                }
                // Remove .slnx files with the same base name as a .sln file
                solutionPaths = solutionPaths.Where(f => !(f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) && duplicateBaseNames.Contains(Path.GetFileNameWithoutExtension(f), StringComparer.OrdinalIgnoreCase))).ToArray();
            }
            return solutionPaths;
        }

        public List<IInspector> CreateInspectors(InspectionOptions options, NugetSearchService nugetService)
        {
            var inspectors = new List<IInspector>();
            if (Directory.Exists(options.TargetPath)) // src dir
            {
                Console.WriteLine("Searching for solution files to process...");
                string[] solutionPaths = FindSolutionFilesTopLevel(options.TargetPath);
                solutionPaths = FilterOutDuplicateXMLSolutionFiles(solutionPaths);

                if (solutionPaths != null && solutionPaths.Length >= 1)
                {
                    foreach (var solution in solutionPaths)
                    {
                        Console.WriteLine("Found Solution {0}", solution);
                        var solutionOp = new SolutionInspectionOptions(options);
                        solutionOp.TargetPath = solution; // path to solution file
                        inspectors.Add(new SolutionInspector(solutionOp, nugetService));
                    }

                }
                else
                {
                    Console.WriteLine("No Solution file found.  Searching for a project file...");
                    string[] projectPaths = SupportedProjectPatterns.AsList.SelectMany(pattern => Directory.GetFiles(options.TargetPath, pattern)).Distinct().ToArray();
                    if (projectPaths != null && projectPaths.Length > 0)
                    {
                        foreach (var projectPath in projectPaths)
                        {
                            Console.WriteLine("Found project {0}", projectPath);
                            var projectOp = new ProjectInspectionOptions(options);
                            projectOp.TargetPath = projectPath;
                            inspectors.Add(new ProjectInspector(projectOp, nugetService));
                        }
                    }
                    else
                    {
                        Console.WriteLine("No Project file found. Finished.");
                    }
                }
            }
            else if (File.Exists(options.TargetPath))
            {
                var fileExtension = Path.GetExtension(options.TargetPath);
                if (string.IsNullOrEmpty(fileExtension))
                {
                    Console.WriteLine($"TargetPath '{options.TargetPath}' does not have a file extension. Skipping.");
                }
                else if (fileExtension.Equals(".sln", StringComparison.OrdinalIgnoreCase) || fileExtension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
                {
                    var solutionOp = new SolutionInspectionOptions(options);
                    solutionOp.TargetPath = options.TargetPath;
                    inspectors.Add(new SolutionInspector(solutionOp, nugetService));
                }
                else
                {
                    var projectOp = new ProjectInspectionOptions(options);
                    projectOp.TargetPath = options.TargetPath;
                    inspectors.Add(new ProjectInspector(projectOp, nugetService));
                }
            }

            return inspectors;
        }
    }
}
