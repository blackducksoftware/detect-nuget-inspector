using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synopsys.Detect.Nuget.Inspector.Inspection
{
    interface IInspector
    {
        InspectionResult Inspect();
    }
}
