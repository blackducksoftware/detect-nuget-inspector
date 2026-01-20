using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Blackduck.Detect.Nuget.Inspector.Inspection.Util;
using Blackduck.Detect.Nuget.Inspector.Result;

namespace Blackduck.Detect.Nuget.Inspector.Inspection
{
    public class InspectorExecutor
    {
        public static InspectorExecutionResult ExecuteInspectors(InspectionOptions options)
        {
            bool anyFailed = false;
            List<InspectionResult>? inspectionResults = null;

            try
            {
                var dispatch = new InspectorDispatch();
                var searchService = new NugetSearchService(options.PackagesRepoUrl, options.NugetConfigPath);
                inspectionResults = dispatch.Inspect(options, searchService);
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
                                writer.WriteInspectedFiles(options.InspectedFilesInfoPath);
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
            }
            catch (Exception e)
            {
                Console.WriteLine("Error iterating inspection results.");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                anyFailed = true;
            }

            if (anyFailed)
            {
                return InspectorExecutionResult.Failed();
            }
            else
            {
                return InspectorExecutionResult.Succeeded(inspectionResults);
            }
        }
    }
}
