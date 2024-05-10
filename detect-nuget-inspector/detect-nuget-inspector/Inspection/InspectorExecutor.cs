using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synopsys.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Synopsys.Detect.Nuget.Inspector.Inspection.Util;
using Synopsys.Detect.Nuget.Inspector.Result;

namespace Synopsys.Detect.Nuget.Inspector.Inspection
{
    public class InspectorExecutor
    {
        public static InspectorExecutionResult ExecuteInspectors(InspectionOptions options)
        {
            IEnumerable<string> lines = File.ReadLines(options.TargetPath);
            bool anyFailed = false;
            var index = 1;
            var originalOutputDirectory = options.OutputDirectory;
            foreach (var targetPath in lines)
            {
                try
                {
                    options.OutputDirectory = new File(originalOutputDirectory, "inspection-" + index);
                    var dispatch = new InspectorDispatch();
                    var searchService = new NugetSearchService(options.PackagesRepoUrl, options.NugetConfigPath);
                    var inspectionResults = dispatch.Inspect(targetPath, options, searchService);
                    if (inspectionResults != null)
                    {
                        foreach (var result in inspectionResults)
                        {
                            try
                            {
                                if (result.ResultName != null)
                                {
                                    Console.WriteLine("Inspection: " + result.ResultName);
                                }
                                if (result.Status == InspectionResult.ResultStatus.Success)
                                {
                                    Console.WriteLine("Inspection Result: Success");
                                    var writer = new InspectionResultJsonWriter(result);
                                    writer.Write();
                                    Console.WriteLine("Info file created at {0}", writer.FilePath());
                                }
                                else
                                {
                                    Console.WriteLine("Inspection Result: Error");
                                    if (result.Exception != null)
                                    {
                                        Console.WriteLine("Exception:");
                                        Console.WriteLine(result.Exception);
                                        anyFailed = true;
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Error processing inspection result.");
                                Console.WriteLine(e.Message);
                                Console.WriteLine(e.StackTrace);
                                anyFailed = true;
                            }
                        }
                    }
                index++;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error iterating inspection results.");
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                    anyFailed = true;
                }
            }
            if (anyFailed)
            {
                return InspectorExecutionResult.Failed();
            }
            else
            {
                return InspectorExecutionResult.Succeeded();
            }
        }
    }
}
