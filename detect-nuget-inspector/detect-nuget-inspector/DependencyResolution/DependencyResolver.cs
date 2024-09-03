using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution
{
    interface DependencyResolver
    {
        DependencyResult Process();
    }
}
