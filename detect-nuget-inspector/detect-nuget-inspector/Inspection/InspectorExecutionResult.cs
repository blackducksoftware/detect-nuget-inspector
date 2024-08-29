using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCA.Detect.Nuget.Inspector.Result
{
    public class InspectorExecutionResult
    {
        public bool Success;
        public int ExitCode = 0;

        public static InspectorExecutionResult Failed(int exitCode = -1)
        {
            return new InspectorExecutionResult()
            {
                Success = false,
                ExitCode = exitCode
            };
        }

        public static InspectorExecutionResult Succeeded()
        {
            return new InspectorExecutionResult
            {
                Success = true
            };
        }
    }
}
