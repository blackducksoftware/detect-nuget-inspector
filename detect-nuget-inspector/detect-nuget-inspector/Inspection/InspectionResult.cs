using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SCA.Detect.Nuget.Inspector.Model;

namespace SCA.Detect.Nuget.Inspector.Inspection
{
    class InspectionResult
    {
        public enum ResultStatus
        {
            Success,
            Error
        }

        public string ResultName;
        public string OutputDirectory;
        public ResultStatus Status;
        public List<Container> Containers = new List<Container>();
        public Exception Exception;

    }
}
