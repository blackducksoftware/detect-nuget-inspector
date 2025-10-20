using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blackduck.Detect.Nuget.Inspector.Inspection;

namespace Blackduck.Detect.Nuget.Inspector.Result
{
    public class InspectorExecutionResult
    {
        public bool Success;
        public int ExitCode = 0;
        public List<InspectionResult>? Results { get; set; }

        public static InspectorExecutionResult Failed(int exitCode = -1)
        {
            return new InspectorExecutionResult()
            {
                Success = false,
                ExitCode = exitCode
            };
        }

        public static InspectorExecutionResult Succeeded(List<InspectionResult>? results)
        {
            var inspectionResults = new InspectorExecutionResult
            {
                Success = true
            };
            inspectionResults.Results = results;
            return inspectionResults;
        }
    }
}
