using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synopsys.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Synopsys.Detect.Nuget.Inspector.Inspection.Inspectors;
using Synopsys.Detect.Nuget.Inspector.Inspection.Util;

namespace Synopsys.Detect.Nuget.Inspector.Inspection
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

        public List<IInspector> CreateInspectors(InspectionOptions options, NugetSearchService nugetService)
        {
            var inspectors = new List<IInspector>();
            if (Directory.Exists(options.TargetPath))
            {
                Console.WriteLine("Searching for solution files to process...");
                string[] solutionPaths = Directory.GetFiles(options.TargetPath, "*.sln");

                if (solutionPaths != null && solutionPaths.Length >= 1)
                {
                    foreach (var solution in solutionPaths)
                    {
                        Console.WriteLine("Found Solution {0}", solution);
                        var solutionOp = new SolutionInspectionOptions(options);
                        solutionOp.TargetPath = solution;
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
                if (options.TargetPath.Contains(".sln"))
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
